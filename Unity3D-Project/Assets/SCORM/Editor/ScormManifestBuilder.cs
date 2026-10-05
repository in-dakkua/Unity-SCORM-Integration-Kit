/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Builds the imsmanifest.xml of a single-SCO SCORM 2004 (3rd or 4th Edition) package
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 **********************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

/// <summary>
/// Pure manifest generator (no UnityEditor dependency): XmlWriter escapes every value and writes numbers with the
/// invariant culture, so the output does not depend on the machine locale.
/// </summary>
public static class ScormManifestBuilder {

	public const string ManifestFileName = "imsmanifest.xml";
	public const string LaunchFile = "index.html";

	const string NsImscp = "http://www.imsglobal.org/xsd/imscp_v1p1";
	const string NsAdlcp = "http://www.adlnet.org/xsd/adlcp_v1p3";
	const string NsAdlseq = "http://www.adlnet.org/xsd/adlseq_v1p3";
	const string NsAdlnav = "http://www.adlnet.org/xsd/adlnav_v1p3";
	const string NsImsss = "http://www.imsglobal.org/xsd/imsss";
	const string NsXsi = "http://www.w3.org/2001/XMLSchema-instance";
	const string NsLom = "http://ltsc.ieee.org/xsd/LOM";

	/// <summary>
	/// Returns the manifest XML.
	/// </summary>
	/// <param name="settings">Package values.</param>
	/// <param name="relativeFiles">Files of the WebGL build relative to the package root ("/" or "\" separated).</param>
	public static string Build(ScormPackageSettings settings, IEnumerable<string> relativeFiles) {
		if (settings == null) throw new ArgumentNullException("settings");
		bool is4th = settings.edition == ScormEdition.Scorm2004_4th;
		bool hasLom = !string.IsNullOrEmpty(settings.courseDescription);

		XmlWriterSettings ws = new XmlWriterSettings();
		ws.Encoding = new UTF8Encoding(false);
		ws.Indent = true;
		ws.IndentChars = "\t";
		ws.NewLineChars = "\n";

		using (MemoryStream ms = new MemoryStream()) {
			using (XmlWriter w = XmlWriter.Create(ms, ws)) {
				w.WriteStartDocument();
				w.WriteStartElement("manifest", NsImscp);
				w.WriteAttributeString("identifier", ScormPackageSettings.SanitizeIdentifier(settings.identifier));
				w.WriteAttributeString("version", "1");
				w.WriteAttributeString("xmlns", "adlcp", null, NsAdlcp);
				w.WriteAttributeString("xmlns", "adlseq", null, NsAdlseq);
				w.WriteAttributeString("xmlns", "adlnav", null, NsAdlnav);
				w.WriteAttributeString("xmlns", "imsss", null, NsImsss);
				w.WriteAttributeString("xmlns", "xsi", null, NsXsi);
				if (hasLom)
					w.WriteAttributeString("xmlns", "lom", null, NsLom);

				string schemaLocation =
					NsImscp + " imscp_v1p1.xsd " +
					NsAdlcp + " adlcp_v1p3.xsd " +
					NsAdlseq + " adlseq_v1p3.xsd " +
					NsAdlnav + " adlnav_v1p3.xsd " +
					NsImsss + " imsss_v1p0.xsd" +
					(hasLom ? " " + NsLom + " lom.xsd" : "");
				w.WriteAttributeString("xsi", "schemaLocation", NsXsi, schemaLocation);

				// metadata
				w.WriteStartElement("metadata", NsImscp);
				w.WriteElementString("schema", NsImscp, "ADL SCORM");
				w.WriteElementString("schemaversion", NsImscp, is4th ? "2004 4th Edition" : "2004 3rd Edition");
				if (hasLom) {
					w.WriteStartElement("lom", "lom", NsLom);
					w.WriteStartElement("general", NsLom);
					w.WriteStartElement("description", NsLom);
					w.WriteStartElement("string", NsLom);
					w.WriteAttributeString("language", string.IsNullOrEmpty(settings.language) ? "en-US" : settings.language);
					w.WriteString(settings.courseDescription);
					w.WriteEndElement();
					w.WriteEndElement();
					w.WriteEndElement();
					w.WriteEndElement();
				}
				w.WriteEndElement();

				// organizations (child order of <item> follows the SCORM CAM)
				w.WriteStartElement("organizations", NsImscp);
				w.WriteAttributeString("default", "ORG-1");
				w.WriteStartElement("organization", NsImscp);
				w.WriteAttributeString("identifier", "ORG-1");
				w.WriteElementString("title", NsImscp, settings.courseTitle ?? "");

				w.WriteStartElement("item", NsImscp);
				w.WriteAttributeString("identifier", "ITEM-1");
				w.WriteAttributeString("identifierref", "RES-1");
				w.WriteAttributeString("isvisible", "true");
				w.WriteElementString("title", NsImscp, settings.scoTitle ?? "");

				if (!string.IsNullOrEmpty(settings.timeLimitAction))
					w.WriteElementString("timeLimitAction", NsAdlcp, settings.timeLimitAction);

				if (!string.IsNullOrEmpty(settings.launchData))
					w.WriteElementString("dataFromLMS", NsAdlcp, settings.launchData);

				if (is4th) {
					if (settings.completedByMeasure) {
						w.WriteStartElement("completionThreshold", NsAdlcp);
						w.WriteAttributeString("completedByMeasure", "true");
						w.WriteAttributeString("minProgressMeasure", ScormFormat.ToReal(Clamp01(settings.minProgressMeasure)));
						w.WriteEndElement();
					}
				} else if (settings.completionThreshold.HasValue) {
					w.WriteElementString("completionThreshold", NsAdlcp, ScormFormat.ToReal(Clamp01(settings.completionThreshold.Value)));
				}

				if (settings.timeLimitSecs > 0f) {
					w.WriteStartElement("sequencing", NsImsss);
					w.WriteStartElement("limitConditions", NsImsss);
					w.WriteAttributeString("attemptAbsoluteDurationLimit", ScormFormat.SecondsToTimeInterval(settings.timeLimitSecs));
					w.WriteEndElement();
					w.WriteEndElement();
				}

				w.WriteEndElement();	// item
				w.WriteEndElement();	// organization
				w.WriteEndElement();	// organizations

				// resources
				w.WriteStartElement("resources", NsImscp);
				w.WriteStartElement("resource", NsImscp);
				w.WriteAttributeString("identifier", "RES-1");
				w.WriteAttributeString("type", "webcontent");
				w.WriteAttributeString("adlcp", "scormType", NsAdlcp, "sco");
				w.WriteAttributeString("href", LaunchFile);
				foreach (string href in NormalizeFiles(relativeFiles)) {
					w.WriteStartElement("file", NsImscp);
					w.WriteAttributeString("href", href);
					w.WriteEndElement();
				}
				w.WriteEndElement();	// resource
				w.WriteEndElement();	// resources

				w.WriteEndElement();	// manifest
				w.WriteEndDocument();
			}
			return new UTF8Encoding(false).GetString(ms.ToArray());
		}
	}

	/// <summary>
	/// "/" separators, URI-escaped segments, no imsmanifest.xml, no .meta, launch file first, no duplicates.
	/// </summary>
	public static List<string> NormalizeFiles(IEnumerable<string> relativeFiles) {
		List<string> result = new List<string>();
		if (relativeFiles == null) return result;
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (string raw in relativeFiles) {
			if (string.IsNullOrEmpty(raw)) continue;
			string path = raw.Replace('\\', '/').TrimStart('/');
			if (path.Length == 0) continue;
			if (string.Equals(path, ManifestFileName, StringComparison.OrdinalIgnoreCase)) continue;
			if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
			string href = string.Join("/", path.Split('/').Select(segment => Uri.EscapeDataString(segment)).ToArray());
			if (seen.Add(href))
				result.Add(href);
		}
		result.Sort(StringComparer.Ordinal);
		int launch = result.IndexOf(LaunchFile);
		if (launch > 0) {
			result.RemoveAt(launch);
			result.Insert(0, LaunchFile);
		}
		return result;
	}

	static float Clamp01(float value) {
		return value < 0f ? 0f : (value > 1f ? 1f : value);
	}
}
