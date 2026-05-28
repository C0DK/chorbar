function randomInRange(min, max) {
  return Math.random() * (max - min) + min;
}

document.addEventListener("click", function (e) {
  var btn = e.target.closest(".do button[type=submit]");
  if (!btn) return;
  var rect = btn.getBoundingClientRect();
  confetti({

    angle: randomInRange(55, 125),
    particleCount: randomInRange(50, 100),
    spread: randomInRange(50, 70),
    origin: {
      x: (rect.left + rect.width / 2) / window.innerWidth,
      y: (rect.top + rect.height / 2) / window.innerHeight
    },
    startVelocity: 25,
    gravity: 1.2,
    ticks: 150
  });
});
