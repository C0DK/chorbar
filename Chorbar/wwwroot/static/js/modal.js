document.addEventListener("toggle", function (e) {
  if (!(e.target instanceof HTMLElement) || !e.target.hasAttribute("popover") || e.target.id !== 'modal') return;

  var open = e.newState === "open";
  var apply = function () {
    document.querySelectorAll("body > :not([popover])").forEach(function (el) {
      el.inert = open;
    });
  };
  if (open) apply();
  else setTimeout(apply, 30); // sometimes the touch thing is too quick and will click through.
}, true);
