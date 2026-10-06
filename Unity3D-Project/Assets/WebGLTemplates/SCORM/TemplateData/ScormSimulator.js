/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 * 
 * Scorm Simulator - Simulates SCORM functionality to test your Unity3D app without loading into an LMS
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
 **********************************************************************************************************************/


/*******************************************************************************
** Usage: Creates javacript object to simulate SCORM LMS. Uncomment lines at bottom for proper LMS version.
**      functions as follows:
**      No error checking. A suspended attempt (cmi.exit = "suspend") is kept in localStorage and resumed on the next
**      launch (cmi.entry = "resume"); open the page with ?scormsimreset=1 to forget it. Use it when testing locally to
**      ensure that the system will successfully initialize.
**
*******************************************************************************/
function scormSimulatorStorageKey()
{
	return "ScormSimulator:" + window.location.pathname;
}

function ScormSimulator()
{
	
	
	this.Initialize = function(s)
	{
			this.errorcode = 0;
			this.data = {};
			this.data["cmi._version"] = "1.0";

			this.data["cmi.comments_from_learner._count"] = "1";
			this.data["cmi.comments_from_learner.0.comment"] = "An example comment from learner";
			this.data["cmi.comments_from_learner.0.location"] = "Location 1";
			this.data["cmi.comments_from_learner.0.timestamp"] = "2015-09-07T09:00:00";
	        this.data["cmi.comments_from_lms._count"] = "1";
            this.data["cmi.comments_from_lms.0.comment"] = "test comment from LMS";            
            this.data["cmi.comments_from_lms.0.location"] = "test location string";
            this.data["cmi.comments_from_lms.0.timestamp"] = "2014-09-07T09:00:00";

            
            this.data["cmi.completion_status"] = "incomplete";
            this.data["cmi.completion_threshold"] = "0.9";
            this.data["cmi.credit"] = "credit";
            this.data["cmi.entry"] = "ab-initio";
            
            
            this.data["cmi.interactions._count"] = "1";
            this.data["cmi.interactions.0.id"] = "urn:STALS:interaction-id-0";
            this.data["cmi.interactions.0.type"] = "true-false";
            this.data["cmi.interactions.0.objectives._count"] = "2";
            this.data["cmi.interactions.0.objectives.0.id"] = "Objective1";
            this.data["cmi.interactions.0.objectives.1.id"] = "Objective2";
            this.data["cmi.interactions.0.timestamp"] = "2015-09-07T09:00:00";
			this.data["cmi.interactions.0.correct_responses._count"] = "1";
			this.data["cmi.interactions.0.correct_responses.0.pattern"] = "true";
            this.data["cmi.interactions.0.weighting"] = "1.0";
            this.data["cmi.interactions.0.learner_response"] = "false";
            this.data["cmi.interactions.0.result"] = "incorrect";
            this.data["cmi.interactions.0.latency"] = "P0DT0H2M18S";
            this.data["cmi.interactions.0.description"] = "Interaction description";
            
            this.data["cmi.launch_data"] = "Launch data from the ims manifest file will be here.";
            
            this.data["cmi.learner_id"] = "rdescartes";            
            this.data["cmi.learner_name"] = "Rene Descartes";
            
			this.data["cmi.learner_preference.audio_captioning"] = "-1";
            this.data["cmi.learner_preference.audio_level"] = "80.0";
            this.data["cmi.learner_preference.delivery_speed"] = "1.0";
            this.data["cmi.learner_preference.language"] = "en-US";
                       
            this.data["cmi.location"] = "Bookmarked location id";
            this.data["cmi.max_time_allowed"] = "P0DT1H0M0S";
            this.data["cmi.mode"] = "normal";

            this.data["cmi.objectives._count"] = "2";
            this.data["cmi.objectives.0.id"] = "Objective1";
            this.data["cmi.objectives.0.score.scaled"] = "0.50";
            this.data["cmi.objectives.0.score.raw"] = "45.0";
            this.data["cmi.objectives.0.score.min"] = "0.0";
            this.data["cmi.objectives.0.score.max"] = "90.0";
            this.data["cmi.objectives.0.success_status"] = "failed";
            this.data["cmi.objectives.0.completion_status"] = "completed";
            this.data["cmi.objectives.0.progress_measure"] = "0.9";            
            this.data["cmi.objectives.0.description"] = "Understand how to use the SCORM API.";
            this.data["cmi.objectives.1.id"] = "Objective2";
            this.data["cmi.objectives.1.score.scaled"] = "0.80";
            this.data["cmi.objectives.1.score.raw"] = "80.0";
            this.data["cmi.objectives.1.score.min"] = "0.0";
            this.data["cmi.objectives.1.score.max"] = "100.0";
            this.data["cmi.objectives.1.success_status"] = "passed";
            this.data["cmi.objectives.1.completion_status"] = "completed";
            this.data["cmi.objectives.1.progress_measure"] = "1";            
            this.data["cmi.objectives.1.description"] = "Understand how to use the SCORM API in Unity3D.";
            
            this.data["cmi.progress_measure"] = "0.3";            
            this.data["cmi.scaled_passing_score"] = "0.8";
            
            this.data["cmi.score.max"] = "100.0";
            this.data["cmi.score.min"] = "0.0";
            this.data["cmi.score.raw"] = "75.5";
            this.data["cmi.score.scaled"] = "0.755";
            
            this.data["cmi.success_status"] = "unknown";
            this.data["cmi.suspend_data"] = "You set the suspend data.";
            
            this.data["cmi.time_limit_action"] = "continue,no message";
            this.data["cmi.total_time"] = "P0DT0H27M10S";

			// Resume a suspended simulated attempt (see Terminate). ?scormsimreset=1 forgets it.
			try
			{
				var key = scormSimulatorStorageKey();
				if (/(^|[?&])scormsimreset=1(&|$)/.test(window.location.search.substring(1)))
					window.localStorage.removeItem(key);
				var saved = window.localStorage.getItem(key);
				if (saved)
				{
					var restored = JSON.parse(saved);
					if (restored && typeof restored === "object")
					{
						this.data = restored;
						this.data["cmi.entry"] = "resume";
						delete this.data["cmi.exit"];
						delete this.data["cmi.session_time"];
					}
				}
			}
			catch (e)
			{
				if (window.console && console.warn) console.warn("[SCORM] ScormSimulator: localStorage not available, no resume.", e);
			}
	
		return "true";
	}
	this.LMSInitialize = this.Initialize;
	
	this.GetValue = function(identifier)
	{
		this.errorcode = 0;
		// An element that exists but is empty ("") is a valid value, not error 401.
		if(Object.prototype.hasOwnProperty.call(this.data, identifier))
			return String(this.data[identifier]);
		
		this.errorcode = 401;
		return "";
	}
	this.LMSGetValue = this.GetValue;
	
	this.SetValue = function(identifier,value)
	{
		this.errorcode = 0;
		this.data[identifier] = value;
		// Like a real LMS, writing cmi.interactions.N.x grows cmi.interactions._count to N+1 (also nested collections).
		var re = /\.(\d+)\./g, m;
		while ((m = re.exec(identifier)) !== null)
		{
			var countKey = identifier.substring(0, m.index) + "._count";
			var count = parseInt(this.data[countKey], 10) || 0;
			if (parseInt(m[1], 10) + 1 > count)
				this.data[countKey] = String(parseInt(m[1], 10) + 1);
		}
		return "true";
	}
	this.LMSSetValue = this.SetValue;
	
	this.Terminate = function(s)
	{
		// Like an LMS: exit = "suspend" keeps the attempt for the next launch (stored in localStorage, per page path);
		// any other exit ends it, so the next launch starts a new attempt from the seed data.
		try
		{
			var key = scormSimulatorStorageKey();
			if (this.data && this.data["cmi.exit"] === "suspend")
				window.localStorage.setItem(key, JSON.stringify(this.data));
			else
				window.localStorage.removeItem(key);
		}
		catch (e)
		{
			if (window.console && console.warn) console.warn("[SCORM] ScormSimulator: could not save the attempt.", e);
		}
	    return "true";
	}
	this.LMSFinish = this.Terminate;
	
	this.GetDiagnostic = function(errorCode)
	{
		return "Not Implemented";
	}
	this.LMSGetDiagnostic = this.GetDiagnostic;
	
	this.GetErrorString = function(errorCode)
	{
		if (String(errorCode) === "0") return "No Error";
		if (String(errorCode) === "401") return "Undefined Data Model Element";
		return "Not Implemented";
	}
	this.LMSGetErrorString = this.GetErrorString;
	
	this.GetLastError = function()
	{
		return String(this.errorcode);
	}
	this.LMSGetLastError = this.GetLastError;
	
	this.Commit = function()
	{
		return "true";
	}
	this.LMSCommit = this.Commit;
}
 	
	// scorm.js installs the simulator automatically when the page is opened with ?scormsim=1 and no LMS API exists.
	// To force it instead, uncomment one of the lines below (never ship a package with them uncommented).

	//Uncomment this line to simulate a SCORM 2004 LMS
    //window.API_1484_11= new ScormSimulator();
	
	//Uncomment this line to simulate a SCORM 1.2 LMS
    //window.API= new ScormSimulator();
      
