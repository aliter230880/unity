import express from "express";
import { createServer as createViteServer } from "vite";
import path from "path";

async function startServer() {
  const app = express();
  const PORT = 3000;

  app.use(express.json());

  // Mock Unity Project Data Store
  let projectState = {
    files: [
      {
        path: "Assets/Scripts/PlayerController.cs",
        content: `using UnityEngine;\n\npublic class PlayerController : MonoBehaviour {\n    public float speed = 5.0f;\n    \n    void Update() {\n        float move = Input.GetAxis("Horizontal") * speed * Time.deltaTime;\n        transform.Translate(move, 0, 0);\n    }\n}`
      },
      {
        path: "Assets/Shaders/Glow.shader",
        content: `Shader "Custom/Glow" {\n    Properties {\n        _Color ("Color", Color) = (1,1,1,1)\n    }\n    SubShader {\n        Tags { "RenderType"="Opaque" }\n        LOD 100\n        Pass {\n            // Shader logic here\n        }\n    }\n}`
      }
    ],
    hierarchy: [
      { id: "1", name: "Main Camera", components: ["Transform", "Camera", "Audio Listener"] },
      { id: "2", name: "Directional Light", components: ["Transform", "Light"] },
      { id: "3", name: "Player", components: ["Transform", "PlayerController", "MeshRenderer"] }
    ]
  };

  // API Routes
  app.get("/api/project", (req, res) => {
    res.json(projectState);
  });

  app.post("/api/project/update", (req, res) => {
    const { files, hierarchy } = req.body;
    if (files) projectState.files = files;
    if (hierarchy) projectState.hierarchy = hierarchy;
    res.json({ status: "success", projectState });
  });

  app.post("/api/project/action", (req, res) => {
    const { action, payload } = req.body;
    
    switch (action) {
      case "ADD_OBJECT":
        projectState.hierarchy.push({
          id: Math.random().toString(36).substr(2, 9),
          name: payload.name || "New GameObject",
          components: payload.components || ["Transform"]
        });
        break;
      case "CREATE_FILE":
        projectState.files.push({
          path: payload.path,
          content: payload.content || ""
        });
        break;
      case "UPDATE_FILE":
        const fileIdx = projectState.files.findIndex(f => f.path === payload.path);
        if (fileIdx !== -1) {
          projectState.files[fileIdx].content = payload.content;
        } else {
          projectState.files.push({ path: payload.path, content: payload.content });
        }
        break;
      case "DELETE_OBJECT":
        projectState.hierarchy = projectState.hierarchy.filter(obj => obj.id !== payload.id);
        break;
    }
    
    res.json({ status: "success", projectState });
  });

  // Vite middleware for development
  if (process.env.NODE_ENV !== "production") {
    const vite = await createViteServer({
      server: { middlewareMode: true },
      appType: "spa",
    });
    app.use(vite.middlewares);
  } else {
    const distPath = path.join(process.cwd(), 'dist');
    app.use(express.static(distPath));
    app.get('*', (req, res) => {
      res.sendFile(path.join(distPath, 'index.html'));
    });
  }

  app.listen(PORT, "0.0.0.0", () => {
    console.log(`Server running on http://localhost:${PORT}`);
  });
}

startServer();
