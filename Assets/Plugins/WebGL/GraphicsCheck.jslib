mergeInto(LibraryManager.library, {
  // Set by index.html before the game loads.
  IsSoftwareRendering: function () {
    return window.psychSoftwareRendering ? 1 : 0;
  },
});
