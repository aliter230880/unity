import { NextResponse } from 'next/server';
import { db } from '@/db';
import { messages, actions } from '@/db/schema';

async function callAgentAI(prompt: string, isErrorFix: boolean, errorLog: string) {
  // Simulate an Agent AI that returns a Plan and Actions
  if (isErrorFix) {
    return {
      plan: "Analyze the error log and fix the missing reference in the script.",
      actions: [
        { 
          type: 'MODIFY_SCRIPT', 
          params: { 
            name: 'PlayerMovement', 
            content: 'using UnityEngine;\nusing UnityEngine.AI;\n\npublic class PlayerMovement : MonoBehaviour {\n    void Update() {\n        NavMeshAgent agent = GetComponent<NavMeshAgent>();\n        agent.SetDestination(transform.position);\n    }\n}' 
          } 
        }
      ],
      message: "I've fixed the compilation error by adding the missing using directive."
    };
  }

  if (prompt.toLowerCase().includes('враг') || prompt.toLowerCase().includes('enemy')) {
    return {
      plan: "1. Create an EnemyAI script with chasing logic. 2. Configure the enemy material to turn red when attacking.",
      actions: [
        { 
          type: 'CREATE_SCRIPT', 
          params: { 
            name: 'EnemyAI', 
            content: 'using UnityEngine;\n\npublic class EnemyAI : MonoBehaviour {\n    public Transform player;\n    public float speed = 3f;\n    void Update() {\n        transform.position = Vector3.MoveTowards(transform.position, player.position, speed * Time.deltaTime);\n    }\n}' 
          } 
        },
        { 
          type: 'MODIFY_PROPERTY', 
          params: { 
            target: 'EnemyPrefab', 
            component: 'MeshRenderer', 
            property: 'material.color', 
            value: 'Red' 
          } 
        }
      ],
      message: "I've implemented the Enemy AI and set their color to red as requested."
    };
  }

  return {
    plan: "Create a basic movement script for the player.",
    actions: [
      { 
        type: 'CREATE_SCRIPT', 
        params: { 
          name: 'PlayerMovement', 
          content: 'using UnityEngine;\n\npublic class PlayerMovement : MonoBehaviour {\n    void Update() {\n        float x = Input.GetAxis(\"Horizontal\");\n        float z = Input.GetAxis(\"Vertical\");\n        transform.Translate(new Vector3(x, 0, z) * Time.deltaTime * 5f);\n    }\n}' 
        } 
      }
    ],
    message: "Player movement script created."
  };
}

export async function POST(req: Request) {
  try {
    const { projectId, prompt, isErrorFix, errorLog } = await req.json();

    if (!projectId || !prompt) {
      return NextResponse.json({ error: 'Project ID and prompt are required' }, { status: 400 });
    }

    const agentResponse = await callAgentAI(prompt, isErrorFix, errorLog);

    // 1. Save User Message
    const [userMsg] = await db.insert(messages).values({
      projectId,
      role: 'user',
      content: prompt,
      isErrorFix: !!isErrorFix,
    }).returning();

    // 2. Save Assistant Response Message
    const [assistantMsg] = await db.insert(messages).values({
      projectId,
      role: 'assistant',
      content: agentResponse.message,
      isErrorFix: !!isErrorFix,
    }).returning();

    // 3. Save individual actions for auditing
    const actionEntries = agentResponse.actions.map(action => ({
      projectId,
      messageId: assistantMsg.id,
      type: action.type,
      parameters: JSON.stringify(action.params),
    }));
    
    if (actionEntries.length > 0) {
      await db.insert(actions).values(actionEntries);
    }

    return NextResponse.json({
      plan: agentResponse.plan,
      actions: agentResponse.actions,
      message: agentResponse.message
    });
  } catch (error) {
    console.error('Chat API Error:', error);
    return NextResponse.json({ error: 'Internal Server Error' }, { status: 500 });
  }
}
