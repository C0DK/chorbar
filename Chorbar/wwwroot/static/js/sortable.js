htmx.onLoad(function (content) {
  var sortables = content.querySelectorAll(".sortable");
  for (var i = 0; i < sortables.length; i++) {
    var sortable = sortables[i];
    var group = sortable.dataset.sortableGroup;
    var sortableInstance = new Sortable(sortable, {
      animation: 150,
      group: group,
      // Make the `.htmx-indicator` unsortable
      filter: ".htmx-indicator, .no-sort",

      preventOnFilter: false,
      handle: ".sortable-handle",

      // Disable sorting on the `end` event
      onAdd: function (evt) {
        this.option("disabled", true);
      },
      onUpdate: function (evt) {
        this.option("disabled", true);
      }
    });

    // Re-enable sorting on the `htmx:afterSwap` event
    sortable.addEventListener("htmx:afterSwap", function () {
      sortableInstance.option("disabled", false);
    });
  }
});
