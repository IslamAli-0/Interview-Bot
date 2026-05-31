const leaderboardBody = document.getElementById("leaderboard-body");
const difficultyEl = document.getElementById("difficulty");
const frozenEl = document.getElementById("frozen");
const questionsAskedEl = document.getElementById("questions-asked");

const adminPasswordInput = document.getElementById("admin-password");
const adminUnlockButton = document.getElementById("admin-unlock");
const adminStatus = document.getElementById("admin-status");
const adminActions = document.getElementById("admin-actions");
const difficultyInput = document.getElementById("difficulty-input");
const setLevelButton = document.getElementById("set-level");
const clearLevelButton = document.getElementById("clear-level");
const testBroadcastButton = document.getElementById("test-broadcast");
const clearLeaderboardButton = document.getElementById("clear-leaderboard");

let adminPassword = "";
let adminUnlocked = false;

function renderLeaderboard(entries) {
  if (!entries.length) {
    leaderboardBody.innerHTML = '<tr><td colspan="4" class="loading">No users yet.</td></tr>';
    return;
  }

  leaderboardBody.innerHTML = entries
    .map((entry, index) => {
      const name = entry.name || "Unknown";
      const answered = entry.answeredCount ?? 0;
      return `<tr><td>${index + 1}</td><td>${name}</td><td>${entry.score}</td><td>${answered}</td></tr>`;
    })
    .join("");
}

function setAdminState(unlocked, message, isError) {
  adminUnlocked = unlocked;
  adminActions.classList.toggle("hidden", !unlocked);
  adminStatus.textContent = message;
  adminStatus.style.color = isError ? "#c45d43" : "";
}

function adminHeaders() {
  return adminPassword ? { "X-Admin-Password": adminPassword } : {};
}

async function verifyAdmin() {
  const response = await fetch("/api/admin/verify", {
    method: "POST",
    headers: adminHeaders(),
  });

  return response.ok;
}

async function setDifficulty() {
  if (!adminUnlocked) {
    return;
  }

  const level = Number.parseInt(difficultyInput.value, 10);
  if (!Number.isInteger(level) || level < 1 || level > 10) {
    setAdminState(true, "Difficulty must be 1-10.", true);
    return;
  }

  const response = await fetch(`/api/settings/level/${level}`, {
    method: "POST",
    headers: adminHeaders(),
  });

  if (!response.ok) {
    setAdminState(true, "Failed to set difficulty.", true);
    return;
  }

  setAdminState(true, "Difficulty locked.", false);
  await loadStats();
}

async function clearDifficulty() {
  if (!adminUnlocked) {
    return;
  }

  const response = await fetch("/api/settings/level", {
    method: "DELETE",
    headers: adminHeaders(),
  });

  if (!response.ok) {
    setAdminState(true, "Failed to clear override.", true);
    return;
  }

  setAdminState(true, "Difficulty auto-cycle enabled.", false);
  await loadStats();
}

async function clearLeaderboard() {
  if (!adminUnlocked) {
    return;
  }

  const confirmed = window.confirm("Clear all scores and answer history?");
  if (!confirmed) {
    return;
  }

  const response = await fetch("/api/admin/leaderboard", {
    method: "DELETE",
    headers: adminHeaders(),
  });

  if (!response.ok) {
    setAdminState(true, "Failed to clear leaderboard.", true);
    return;
  }

  setAdminState(true, "Leaderboard cleared.", false);
  await loadStats();
}

async function sendTestBroadcast() {
  if (!adminUnlocked) {
    return;
  }

  const response = await fetch("/api/admin/broadcast-test", {
    method: "POST",
    headers: adminHeaders(),
  });

  if (!response.ok) {
    setAdminState(true, "Failed to send test broadcast.", true);
    return;
  }

  let sent = 0;
  try {
    const payload = await response.json();
    sent = payload.sent ?? 0;
  } catch (error) {
    sent = 0;
  }

  setAdminState(true, `Test broadcast sent to ${sent} subscribers.`, false);
}

async function loadStats() {
  try {
    const response = await fetch("/api/dashboard/stats");
    if (!response.ok) {
      throw new Error("Request failed");
    }

    const data = await response.json();
    difficultyEl.textContent = `Level ${data.activeDifficultyLevel}`;
    frozenEl.textContent = data.isDifficultyFrozen ? "Difficulty locked" : "Auto-cycling";
    questionsAskedEl.textContent = data.totalQuestionsAsked ?? "--";

    renderLeaderboard(data.leaderboard || []);
  } catch (error) {
    leaderboardBody.innerHTML = '<tr><td colspan="4" class="loading">Failed to load stats.</td></tr>';
    difficultyEl.textContent = "--";
    frozenEl.textContent = "Unavailable";
    questionsAskedEl.textContent = "--";
  }
}

document.addEventListener("DOMContentLoaded", () => {
  loadStats();
  setAdminState(false, "Locked", false);

  adminUnlockButton.addEventListener("click", async () => {
    const candidate = adminPasswordInput.value.trim();
    if (!candidate) {
      setAdminState(false, "Enter the admin password.", true);
      return;
    }

    adminPassword = candidate;
    const ok = await verifyAdmin();
    if (!ok) {
      adminPassword = "";
      setAdminState(false, "Invalid password.", true);
      return;
    }

    setAdminState(true, "Unlocked", false);
  });

  setLevelButton.addEventListener("click", setDifficulty);
  clearLevelButton.addEventListener("click", clearDifficulty);
  testBroadcastButton.addEventListener("click", sendTestBroadcast);
  clearLeaderboardButton.addEventListener("click", clearLeaderboard);
});
