/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Scorm Javascript layer
 *
 * Copyright (C) 2015, Richard Stals (http://stals.com.au)
 * ==========================================
 *
 *
 * Derived from:
 * Unity-SCORM Integration Toolkit Version 1.0 Beta
 * ==========================================
 *
 * Copyright (C) 2011, by ADL (Advance Distributed Learning). (http://www.adlnet.gov)
 * http://www.adlnet.gov/UnityScormIntegration/
 *
 ***********************************************************************************************************************
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
 * an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations under the License.
 *
 ***********************************************************************************************************************
 *
 * All functions return their result synchronously; Plugins/scorm.jslib passes it straight back to C#.
 *
 * Local testing without an LMS: open index.html?scormsim=1 . The ScormSimulator (TemplateData/ScormSimulator.js) is
 * only installed when that parameter is present AND no LMS API is found, so it can never shadow a real LMS.
 *
 * Window close: on visibilitychange->hidden the data is committed; on pagehide, if Unity did not terminate the
 * session, cmi.session_time (if Unity did not set it) and cmi.exit = "suspend" (if Unity did not set it) are written
 * and the session is committed and terminated.
 *
 **********************************************************************************************************************/


///Rob Chadwick - 6/1/12 - default to false
var debug = false;  // set this to true to log every API call to the console.

var output = window.console; // output can be set to any object that has a log(string) function
                             // such as: var output = { log: function(str){alert(str);} };

// Define exception/error codes
var _NoError = {"code":"0","string":"No Error","diagnostic":"No Error"};
var _GeneralException = {"code":"101","string":"General Exception","diagnostic":"General Exception"};
var _AlreadyInitialized = {"code":"103","string":"Already Initialized","diagnostic":"Already Initialized"};

// Initialized state of the content; default false
var initialized = false;

// True once Terminate has been called: SCORM forbids initializing again in the same page
var terminated = false;

// Handle to the SCORM API instance, and whether the search has already been done
var apiHandle = null;
var apiSearched = false;

// Global for SCORM version; Default 2004 and set when the API is found
var versionIsSCORM2004 = true;

// Session bookkeeping used by the pagehide handler
var initializeTime = null;
var unitySetExit = false;
var unitySetSessionTime = false;

/*******************************************************************************
** Simulator opt-in: index.html?scormsim=1
*******************************************************************************/
function isSimulatorRequested()
{
   try
   {
      return /(^|[?&])scormsim=1(&|$)/.test(window.location.search.substring(1));
   }
   catch (e)
   {
      return false;
   }
}

/*******************************************************************************
**
** Function: doInitialize()
** Return:  "true" if the initialization was successful, or "false" otherwise.
**
*******************************************************************************/
function doInitialize()
{
   if (initialized) return "true";
   if (terminated) return "false";

   var api = getAPIHandle();
   if (api == null)
   {
      message("Unable to locate the LMS's API Implementation.\nInitialize was not successful.");
      return "false";
   }

   var result;
   if (versionIsSCORM2004 == true)
   {
      result = api.Initialize("");
   }
   else
   {
      result = api.LMSInitialize("");
   }

   if (String(result) !== "true")
   {
      var err = ErrorHandler();
      message("Initialize failed with error code: " + err.code);
      return "false";
   }

   initialized = true;
   initializeTime = new Date();
   return "true";
}

/*******************************************************************************
**
** Function doTerminate()
** Return:  "true" if successful, "false" if failed.
**
** Description:
** Close communication with the LMS. Never terminates twice and never closes the
** window (the LMS owns the window).
**
*******************************************************************************/
function doTerminate()
{
   if (terminated || !initialized) return "true";

   var api = getAPIHandle();
   if (api == null)
   {
      message("Unable to locate the LMS's API Implementation.\nTerminate was not successful.");
      return "false";
   }

   var result;
   if (versionIsSCORM2004 == true)
   {
      result = api.Terminate("");
   }
   else
   {
      result = api.LMSFinish("");
   }

   if (String(result) !== "true")
   {
      var err = ErrorHandler();
      message("Terminate failed with error code: " + err.code);
      return "false";
   }

   terminated = true;
   initialized = false;
   return "true";
}

/*******************************************************************************
**
** Function ensureInitialized()
** Return:  true if communication with the LMS is open.
**
*******************************************************************************/
function ensureInitialized(caller)
{
   if (initialized) return true;
   if (terminated) return false;
   if (doInitialize() === "true") return true;
   var err = ErrorHandler();
   message(caller + " failed - Could not initialize communication with the LMS - error code: " + err.code);
   return false;
}

/*******************************************************************************
**
** Function doGetValue(identifier)
** Return:  The value presently assigned by the LMS, or "" on error / without LMS.
**
*******************************************************************************/
function doGetValue(identifier)
{
   // JP TODO - temp hack to get rid of the value for strings and associated language data model elements
   var dotBindingName = identifier.replace(".Value", "");
   var api = getAPIHandle();

   if (api == null)
   {
      message("Unable to locate the LMS's API Implementation.\nGetValue was not successful.");
      return "";
   }
   if (!ensureInitialized("GetValue")) return "";

   var result;
   if (versionIsSCORM2004 == true)
   {
      result = api.GetValue(dotBindingName);
   }
   else
   {
      result = api.LMSGetValue(dotBindingName);
   }

   var error = ErrorHandler();
   if (error.code != _NoError.code)
   {
      message("GetValue(" + dotBindingName + ") failed. \n" + error.code + ": " + error.string);
      return "";
   }

   ///Rob Chadwick - 6/1/12 - some broken LMSs return null
   return (result === null || result === undefined) ? "" : String(result);
}

/*******************************************************************************
**
** Function doSetValue(identifier, value)
** Return:  "true" if successful, "false" if failed.
**
*******************************************************************************/
function doSetValue(identifier, value)
{
   // JP TODO - temp hack to get rid of the value for strings and associated language data model elements
   var dotBindingName = identifier.replace(".Value", "");
   var api = getAPIHandle();

   if (api == null)
   {
      message("Unable to locate the LMS's API Implementation.\nSetValue was not successful.");
      return "false";
   }
   if (!ensureInitialized("SetValue")) return "false";

   var result;
   if (versionIsSCORM2004)
   {
      result = api.SetValue(dotBindingName, value);
   }
   else
   {
      result = api.LMSSetValue(dotBindingName, value);
   }

   if (String(result) !== "true")
   {
      var err = ErrorHandler();
      message("SetValue(" + dotBindingName + ", " + value + ") failed. \n" + err.code + ": " + err.string);
      return "false";
   }

   if (dotBindingName === "cmi.exit" || dotBindingName === "cmi.core.exit") unitySetExit = true;
   if (dotBindingName === "cmi.session_time" || dotBindingName === "cmi.core.session_time") unitySetSessionTime = true;
   return "true";
}

/*******************************************************************************
**
** Function doIsApiFound()
** Return:  true if an LMS API (or the opt-in simulator) is available.
**
*******************************************************************************/
function doIsApiFound()
{
   return getAPIHandle() != null;
}

/*******************************************************************************
**
** Function doIsScorm2004()
** Return:  true if the api is a 2004 api (also true when there is no API),
**          false if the api is a 1.2 api.
**
*******************************************************************************/
function doIsScorm2004()
{
   getAPIHandle();
   return versionIsSCORM2004 == true;
}

/*******************************************************************************
**
** Function doCommit()
** Return:  "true" if successful, "false" if failed.
**
*******************************************************************************/
function doCommit()
{
   var api = getAPIHandle();
   if (api == null)
   {
      message("Unable to locate the LMS's API Implementation.\nCommit was not successful.");
      return "false";
   }
   if (!ensureInitialized("Commit")) return "false";

   var result;
   if (versionIsSCORM2004)
   {
      result = api.Commit("");
   }
   else
   {
      result = api.LMSCommit("");
   }

   if (String(result) !== "true")
   {
      var err = ErrorHandler();
      message("Commit failed - error code: " + err.code);
      return "false";
   }
   return "true";
}

/*******************************************************************************
**
** Function doGetLastError()
** Return:  The error code that was set by the last LMS function call
**
*******************************************************************************/
function doGetLastError()
{
   var api = getAPIHandle();
   if (api == null)
   {
      //since we can't get the error code from the LMS, return a general error
      return _GeneralException.code;
   }

   if (versionIsSCORM2004 == true)
   {
      return String(api.GetLastError());
   }
   return String(api.LMSGetLastError());
}

/*******************************************************************************
**
** Function doGetErrorString(errorCode)
** Return:  The textual description that corresponds to the input error code
**
********************************************************************************/
function doGetErrorString(errorCode)
{
   var api = getAPIHandle();
   if (api == null)
   {
      return _GeneralException.string;
   }

   if (versionIsSCORM2004)
   {
      return String(api.GetErrorString(String(errorCode)));
   }
   return String(api.LMSGetErrorString(String(errorCode)));
}

/*******************************************************************************
**
** Function doGetDiagnostic(errorCode)
** Return:  The vendor specific textual description of the error
**
*******************************************************************************/
function doGetDiagnostic(errorCode)
{
   var api = getAPIHandle();
   if (api == null)
   {
      return "Unable to locate the LMS's API Implementation. GetDiagnostic was not successful.";
   }

   if (versionIsSCORM2004)
   {
      return String(api.GetDiagnostic(errorCode));
   }
   return String(api.LMSGetDiagnostic(errorCode));
}

/*******************************************************************************
**
** Function ErrorHandler()
** Return:  The current error {code, string, diagnostic}
**
*******************************************************************************/
function ErrorHandler()
{
   var error = {"code":_NoError.code, "string":_NoError.string, "diagnostic":_NoError.diagnostic};
   var api = getAPIHandle();
   if (api == null)
   {
      error.code = _GeneralException.code;
      error.string = _GeneralException.string;
      error.diagnostic = "Unable to locate the LMS's API Implementation. Cannot determine LMS error code.";
      return error;
   }

   if (versionIsSCORM2004 == true)
   {
      error.code = String(api.GetLastError());
   }
   else
   {
      error.code = String(api.LMSGetLastError());
   }

   if (error.code != _NoError.code)
   {
      if (versionIsSCORM2004 == true)
      {
         error.string = api.GetErrorString(error.code);
         error.diagnostic = api.GetDiagnostic(null);
      }
      else
      {
         error.string = api.LMSGetErrorString(error.code);
         error.diagnostic = api.LMSGetDiagnostic(null);
      }
   }

   return error;
}

/******************************************************************************
**
** Function getAPIHandle()
** Return:  The API object, or null. The search is done only once per page.
**
*******************************************************************************/
function getAPIHandle()
{
   if (apiHandle == null && !apiSearched)
   {
      apiSearched = true;
      apiHandle = getAPI();
   }
   return apiHandle;
}

/*******************************************************************************
**
** Function findAPI(win)
** Return:  The API found in win or one of its parents, or null.
**
** Description:
** In each window API_1484_11 (SCORM 2004) is preferred over API (SCORM 1.2).
** Access to a cross-origin parent throws; that ends the search instead of
** breaking the content.
**
*******************************************************************************/
function findAPI(win)
{
   var findAPITries = 0;
   while (win != null)
   {
      try
      {
         if (win.API_1484_11 != null)
         {
            versionIsSCORM2004 = true;
            return win.API_1484_11;
         }
         if (win.API != null)
         {
            versionIsSCORM2004 = false;
            return win.API;
         }
      }
      catch (e)
      {
         message("findAPI: cannot access window (cross-origin): " + e);
      }

      var parentWin = null;
      try
      {
         parentWin = win.parent;
      }
      catch (e)
      {
         parentWin = null;
      }
      if (parentWin == null || parentWin === win) break;

      findAPITries++;
      if (findAPITries > 500)
      {
         message("Error finding API -- too deeply nested.");
         return null;
      }
      win = parentWin;
   }
   return null;
}

/*******************************************************************************
**
** Function getAPI()
** Return:  The API found in the frame hierarchy, then in the opener hierarchy;
**          the simulator if ?scormsim=1 and no LMS API exists; otherwise null.
**
*******************************************************************************/
function getAPI()
{
   var theAPI = findAPI(window);
   if (theAPI == null)
   {
      var opener = null;
      try { opener = window.opener; } catch (e) { opener = null; }
      if (opener != null && typeof(opener) != "undefined")
      {
         theAPI = findAPI(opener);
      }
   }

   if (theAPI == null && isSimulatorRequested() && typeof ScormSimulator === "function")
   {
      window.API_1484_11 = new ScormSimulator();
      versionIsSCORM2004 = true;
      theAPI = window.API_1484_11;
      if (window.console && console.warn) console.warn("[SCORM] No LMS API found: using ScormSimulator (?scormsim=1).");
   }

   if (theAPI == null)
   {
      versionIsSCORM2004 = true;
      message("Unable to find an API adapter");
   }

   return theAPI;
}

/*******************************************************************************
**
** Function message(str)
** Description: Outputs debug messages when debug == true.
*******************************************************************************/
function message(str)
{
   if (debug)
   {
      ///Rob Chadwick - 6/1/12 - Fixed bug where console.log is sometimes not available
      if (output && output.log)
         output.log(str);
   }
}

/*******************************************************************************
** Debug output from Unity (ScormAPIWrapper.DebugPrint)
*******************************************************************************/
function DebugPrint(str)
{
   var el = document.getElementById('console');
   if (el)
   {
      el.innerHTML = el.innerHTML + str + "<br />";
   }
   else if (window.console && console.log)
   {
      console.log("[SCORM] " + str);
   }
}

/*******************************************************************************
** Window close handling
*******************************************************************************/
function secondsToTimeInterval(totalSeconds)
{
   var hundredths = Math.max(0, Math.round(totalSeconds * 100));
   var hours = Math.floor(hundredths / 360000); hundredths -= hours * 360000;
   var minutes = Math.floor(hundredths / 6000); hundredths -= minutes * 6000;
   var seconds = hundredths / 100;
   return "PT" + hours + "H" + minutes + "M" + seconds + "S";
}

function scormSessionIsOpen()
{
   return initialized && !terminated && getAPIHandle() != null;
}

document.addEventListener("visibilitychange", function ()
{
   // Synchronous XHR is still allowed here (Chrome blocks it in pagehide), so commit now.
   if (document.visibilityState === "hidden" && scormSessionIsOpen())
   {
      doCommit();
   }
});

window.addEventListener("pagehide", function ()
{
   if (!scormSessionIsOpen()) return;

   if (versionIsSCORM2004)
   {
      if (!unitySetSessionTime && initializeTime != null)
      {
         doSetValue("cmi.session_time", secondsToTimeInterval((new Date() - initializeTime) / 1000));
      }
      if (!unitySetExit)
      {
         doSetValue("cmi.exit", "suspend");
      }
   }
   doCommit();
   doTerminate();
});
