import os
import requests

# --- НАСТРОЙКИ ---
UNITY_PROJECT_PATH = r"D:\Work\Unity\Projects\ava1" # ЗАМЕНИ НА СВОЙ ПУТЬ
BRIDGE_URL = "http://localhost:8080"
# -----------------

def scan_project():
    print("🔍 Сканирую проект Unity...")
    files_map = {}
    
    # Обходим все папки проекта
    for root, dirs, files in os.walk(UNITY_PROJECT_PATH):
        for file in files:
            if file.endswith(".cs"): # Нас интересуют только скрипты
                full_path = os.path.join(root, file)
                # Получаем путь относительно папки проекта (как в Unity)
                relative_path = os.path.relpath(full_path, UNITY_PROJECT_PATH)
                files_map[relative_path] = "C# Script"
    
    print(f"✅ Найдено скриптов: {len(files_map)}")
    return files_map

def test_connection():
    try:
        response = requests.get(f"{BRIDGE_URL}/?cmd=errors")
        if response.status_code == 200:
            print("🌐 Связь с Unity установлена!")
            return True
    except:
        print("❌ Ошибка: Unity Bridge не запущен! Нажми 'Start Bridge Server' в Unity.")
        return False

if __name__ == "__main__":
    # 1. Проверяем, запущен ли сервер в Unity
    if test_connection():
        # 2. Индексируем файлы
        project_files = scan_project()
        
        # Выводим список для проверки
        print("\n--- Карта проекта ---")
        for path in project_files:
            print(path)
        print("\n🚀 Теперь AI знает все твои файлы!")