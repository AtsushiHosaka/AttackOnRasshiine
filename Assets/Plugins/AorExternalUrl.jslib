mergeInto(LibraryManager.library, {
  AOR_OpenExternalUrlNoOpener: function (urlPointer) {
    var url = UTF8ToString(urlPointer);
    var opened = window.open(url, "_blank", "noopener,noreferrer");
    if (opened) {
      opened.opener = null;
    }
  }
});
