const overlay = document.getElementById('hype-train-overlay');
const bar = document.getElementById('hype-train-fill');

let hypeTrainInterval = null;
let hypeTrainEndsAt = null;
let lastExpiresAt = null;
var connectionState = false;
var hypetrainType = "hype train";

const client = new StreamerbotClient({
    port: 8080,
    immediate: true,
    autoReconnect: true,
    onConnect: (data) => {
        console.log(data)
        overlay.classList.add('visible');
    },
    onDisconnect: (data) => {
        console.log(data)
        connectionState = false;
    },
    onError: (err) => {
        console.error(err);
        connectionState = false;
    },
});

client.on('Twitch.StreamOnline', async (payload) => {
    overlay.classList.remove('visible');
})

client.on('Twitch.HypeTrainStart', async (payload) => {
  console.log(payload)
  if (payload.data.is_golden_kappa_train === true) {
    bar.style.background = "#FFCC00";
    hypetrainType = "GOLDEN KAPPA TRAIN"
  }
  else if (payload.data.is_treasure_train) {
    bar.style.background = "#FFCC00";
    hypetrainType = "TREASURE TRAIN"
  }
  else {
    bar.style.background = "#9146FF";
    hypetrainType = "HYPE TRAIN"
  }

  overlay.classList.add('visible');
  handleHypeTrain(payload)
})

client.on('Twitch.HypeTrainUpdate', async (payload) => {
  handleHypeTrain(payload)
})

client.on('Twitch.HypeTrainLevelUp', async (payload) => {
  console.log(payload)
  handleHypeTrain(payload)
})

client.on('Twitch.HypeTrainEnd', async (payload) => {
  clearInterval(hypeTrainInterval);

  hypeTrainInterval = null;
  hypeTrainEndsAt = null;
  lastExpiresAt = null;

  overlay.classList.remove('visible');
  console.log(payload)
})

function formatTime(seconds) {
  const m = Math.floor(seconds / 60).toString().padStart(2, '0');
  const s = Math.floor(seconds % 60).toString().padStart(2, '0');
  return `${m}:${s}`;
}

function startHypeTrainTimer(expiresAtIso) {
  hypeTrainEndsAt = new Date(expiresAtIso).getTime();

  clearInterval(hypeTrainInterval);
  hypeTrainInterval = setInterval(updateHypeTrainTimer, 500);

  updateHypeTrainTimer();
}

function updateHypeTrainTimer() {
  if (!hypeTrainEndsAt) return;

  const remainingMs = hypeTrainEndsAt - Date.now();

  if (remainingMs <= 0) {
    document.getElementById('hype-timer').innerText = '0:00';
    clearInterval(hypeTrainInterval);
    overlay.classList.remove('visible');
    return;
  }

  const totalSeconds = Math.floor(remainingMs / 1000);
  const minutes = Math.floor(totalSeconds / 60)
    .toString()
    .padStart(1, '0');
  const seconds = (totalSeconds % 60)
    .toString()
    .padStart(2, '0');

  document.getElementById('hype-timer').innerText = `${minutes}:${seconds}`;
}

function syncHypeTrainTimer(expiresAtIso) {
  if (!expiresAtIso) return;

  if (expiresAtIso !== lastExpiresAt) {
    lastExpiresAt = expiresAtIso;
    hypeTrainEndsAt = new Date(expiresAtIso).getTime();

    clearInterval(hypeTrainInterval);
    hypeTrainInterval = setInterval(updateHypeTrainTimer, 500);

    updateHypeTrainTimer();
  }
}

function handleHypeTrain(payload) {
  const { progress, goal, level, expires_at } = payload.data;

  document.getElementById('hype-train-overlay').style.display = 'block';

  const percent = Math.min(100, (progress / goal) * 100);
  document.getElementById('hype-train-fill').style.width = `${percent}%`;
  document.getElementById('hype-percent').innerText = `${Math.floor(percent)}%`;
  document.getElementById('hype-level').innerText = `${hypetrainType}\nLEVEL ${level}`;


  syncHypeTrainTimer(expires_at);
}