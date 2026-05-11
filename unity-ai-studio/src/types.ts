export interface UnityObject {
  id: string;
  name: string;
  components: string[];
}

export interface UnityFile {
  path: string;
  content: string;
}

export interface ProjectState {
  files: UnityFile[];
  hierarchy: UnityObject[];
}

export type ToolResponse = {
  action: "ADD_OBJECT" | "CREATE_FILE" | "DELETE_OBJECT" | "UPDATE_FILE";
  payload: any;
};
