/*
 * Unity-SCORM Integration Kit - WebGL bridge.
 *
 * The SCORM 2004 JavaScript API is synchronous, so every function returns its result directly to C#.
 * Strings are returned as a UTF-8 buffer allocated with _malloc; IL2CPP frees it after marshalling the
 * "extern string" return value.
 *
 * The do* functions live in TemplateData/scorm.js (loaded in <head> before the Unity loader). They are looked up
 * on window at call time, by string name, so minification of this library cannot break the lookup.
 */
var ScormWebGL = {

   wgldebugPrint: function (str) {
      var text = UTF8ToString(str);
      if (typeof window["DebugPrint"] === "function") window["DebugPrint"](text);
      else if (window.console) console.log("[SCORM] " + text);
   },

   wglInitialize: function () {
      if (typeof window["doInitialize"] !== "function") return 0;
      return String(window["doInitialize"]()) === "true" ? 1 : 0;
   },

   wglIsApiFound: function () {
      if (typeof window["doIsApiFound"] !== "function") return 0;
      return window["doIsApiFound"]() ? 1 : 0;
   },

   wglIsScorm2004: function () {
      if (typeof window["doIsScorm2004"] !== "function") return 1;
      return window["doIsScorm2004"]() === false ? 0 : 1;
   },

   wglGetValue: function (identifier) {
      var result = "";
      if (typeof window["doGetValue"] === "function") result = window["doGetValue"](UTF8ToString(identifier));
      var str = (result === null || result === undefined) ? "" : String(result);
      var size = lengthBytesUTF8(str) + 1;
      var buffer = _malloc(size);
      stringToUTF8(str, buffer, size);
      return buffer;
   },

   wglSetValue: function (identifier, value) {
      if (typeof window["doSetValue"] !== "function") return 0;
      return String(window["doSetValue"](UTF8ToString(identifier), UTF8ToString(value))) === "true" ? 1 : 0;
   },

   wglCommit: function () {
      if (typeof window["doCommit"] !== "function") return 0;
      return String(window["doCommit"]()) === "true" ? 1 : 0;
   },

   wglTerminate: function () {
      if (typeof window["doTerminate"] !== "function") return 0;
      return String(window["doTerminate"]()) === "true" ? 1 : 0;
   },

   wglGetLastError: function () {
      if (typeof window["doGetLastError"] !== "function") return 0;
      var code = parseInt(window["doGetLastError"](), 10);
      return isNaN(code) ? 0 : code;
   },

   wglGetErrorString: function (errorCode) {
      var result = "";
      if (typeof window["doGetErrorString"] === "function") result = window["doGetErrorString"](String(errorCode));
      var str = (result === null || result === undefined) ? "" : String(result);
      var size = lengthBytesUTF8(str) + 1;
      var buffer = _malloc(size);
      stringToUTF8(str, buffer, size);
      return buffer;
   }
};

mergeInto(LibraryManager.library, ScormWebGL);
