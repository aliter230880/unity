const $ = (id) => document.getElementById(id);

let state = null;

async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json" },
    ...options,
  });
  const text = await res.text();
  const data = text ? JSON.parse(text) : {};
  if (!res.ok) throw new Error(data.error || text || "Request failed");
  return data;
}

function formatDate(value) {
  if (!value) return "нет";
  try {
    return new Date(value).toLocaleString();
  } catch {
    return value;
  }
}

function addMessage(text, kind = "assistant") {
  const div = document.createElement("div");
  div.className = `bubble ${kind}`;
  div.textContent = text;
  $("messages").appendChild(div);
  $("messages").scrollTop = $("messages").scrollHeight;
}

function commandLabel(command) {
  if (command.type === "write_file" || command.type === "create_script") {
    return `${command.type}: ${command.path}`;
  }
  if (command.type === "create_gameobject") {
    return `create_gameobject: ${command.name || "GameObject"}`;
  }
  if (command.type === "add_component") {
    return `add_component: ${command.component} -> ${command.target}`;
  }
  if (command.type === "set_transform") {
    return `set_transform: ${command.target}`;
  }
  return command.type;
}

function renderProposals(proposals) {
  const list = $("proposalList");
  list.innerHTML = "";

  if (!proposals || proposals.length === 0) {
    const empty = document.createElement("p");
    empty.className = "hint";
    empty.textContent = "Пока нет предложений. Напиши задачу в чат.";
    list.appendChild(empty);
    return;
  }

  for (const proposal of proposals) {
    const card = document.createElement("div");
    card.className = "proposalCard";

    const top = document.createElement("div");
    top.className = "proposalTop";

    const text = document.createElement("div");
    text.innerHTML = `<strong>${proposal.userMessage || "Изменение"}</strong><p class="hint">${proposal.reply || ""}</p>`;
    top.appendChild(text);

    if (proposal.status === "proposed") {
      const button = document.createElement("button");
      button.textContent = "Apply";
      button.onclick = async () => {
        button.disabled = true;
        try {
          await api("/api/proposals/apply", {
            method: "POST",
            body: JSON.stringify({ id: proposal.id }),
          });
          addMessage("Отправил команды в Unity. В плагине должен быть включен Poll Commands.", "assistant");
          await loadState();
        } catch (error) {
          addMessage(error.message, "error");
        } finally {
          button.disabled = false;
        }
      };
      top.appendChild(button);
    }

    card.appendChild(top);

    const ul = document.createElement("ul");
    for (const command of proposal.commands || []) {
      const li = document.createElement("li");
      li.textContent = commandLabel(command);
      ul.appendChild(li);
    }
    card.appendChild(ul);
    list.appendChild(card);
  }
}

async function loadState() {
  state = await api("/api/state");
  $("projectName").textContent = state.project.projectName || "-";
  $("fileCount").textContent = String(state.project.fileCount || 0);
  $("sceneName").textContent = state.project.sceneName || "-";
  $("lastSync").textContent = formatDate(state.project.lastSync);
  $("baseUrl").value = state.settings.baseUrl || "";
  $("model").value = state.settings.model || "";
  $("autoApply").checked = Boolean(state.settings.autoApply);
  $("settingsStatus").textContent = state.settings.hasApiKey ? "Ключ сохранен локально." : "Ключ еще не сохранен.";
  renderProposals(state.proposals);
}

$("saveSettings").onclick = async () => {
  $("saveSettings").disabled = true;
  try {
    await api("/api/settings", {
      method: "POST",
      body: JSON.stringify({
        baseUrl: $("baseUrl").value,
        model: $("model").value,
        apiKey: $("apiKey").value,
        autoApply: $("autoApply").checked,
      }),
    });
    $("apiKey").value = "";
    await loadState();
    addMessage("Настройки сохранены.", "assistant");
  } catch (error) {
    addMessage(error.message, "error");
  } finally {
    $("saveSettings").disabled = false;
  }
};

$("refreshState").onclick = loadState;

$("chatForm").onsubmit = async (event) => {
  event.preventDefault();
  const message = $("message").value.trim();
  if (!message) return;

  $("message").value = "";
  addMessage(message, "user");
  $("sendMessage").disabled = true;

  try {
    const data = await api("/api/chat", {
      method: "POST",
      body: JSON.stringify({ message }),
    });
    addMessage(data.proposal.reply || "Готово.", "assistant");
    await loadState();
  } catch (error) {
    addMessage(error.message, "error");
  } finally {
    $("sendMessage").disabled = false;
  }
};

loadState().catch((error) => addMessage(error.message, "error"));

