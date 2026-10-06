/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 *
 * Packages a WebGL build as a SCORM 2004 zip (imsmanifest.xml + XSD at the root) and validates manifests
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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;

/// <summary>Result of ScormPackager.Package.</summary>
public class ScormPackageResult {
	public string zipPath;
	public string xsdDirectory;
	public int contentFileCount;
	public List<string> warnings = new List<string>();
}

/// <summary>
/// Creates the SCORM zip with System.IO.Compression (entries always use "/") and validates manifests against the XSD.
/// </summary>
public static class ScormPackager {

	/// <summary>Folder (relative to the project) that holds 2004_3rd/ and 2004_4th/.</summary>
	public const string DefaultXsdRoot = "Assets/SCORM/Plugins";

	/// <summary>
	/// XSD folder for an edition. 3rd Edition falls back to the 4th Edition set (with a warning) while
	/// Plugins/2004_3rd is missing or contains no .xsd file.
	/// </summary>
	public static string ResolveXsdDirectory(string xsdRoot, ScormEdition edition, out string warning) {
		warning = null;
		string dir4th = Path.Combine(xsdRoot, "2004_4th");
		if (edition == ScormEdition.Scorm2004_4th)
			return dir4th;
		string dir3rd = Path.Combine(xsdRoot, "2004_3rd");
		if (Directory.Exists(dir3rd) && Directory.GetFiles(dir3rd, "*.xsd", SearchOption.TopDirectoryOnly).Length > 0)
			return dir3rd;
		warning = "SCORM 2004 3rd Edition XSD files not found in " + dir3rd.Replace('\\', '/') +
			"; the 3rd Edition package uses the 4th Edition XSD set instead (not strictly 3rd Edition conformant).";
		return dir4th;
	}

	/// <summary>Files of a folder relative to it ("/" separated), without .meta, hidden files or imsmanifest.xml.</summary>
	public static List<string> ListContentFiles(string directory) {
		string root = Path.GetFullPath(directory).TrimEnd('\\', '/');
		List<string> files = new List<string>();
		foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) {
			string relative = file.Substring(root.Length + 1).Replace('\\', '/');
			if (IsExcluded(relative)) continue;
			if (string.Equals(relative, ScormManifestBuilder.ManifestFileName, StringComparison.OrdinalIgnoreCase)) continue;
			files.Add(relative);
		}
		files.Sort(StringComparer.Ordinal);
		return files;
	}

	static bool IsExcluded(string relative) {
		if (relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;
		foreach (string segment in relative.Split('/'))
			if (segment.StartsWith(".", StringComparison.Ordinal)) return true;
		return false;
	}

	/// <summary>
	/// Creates zipPath with: the WebGL build in buildDir, imsmanifest.xml and the XSD set of the edition, all at the root.
	/// </summary>
	public static ScormPackageResult Package(string buildDir, string zipPath, ScormPackageSettings settings, string xsdRoot = DefaultXsdRoot) {
		if (!Directory.Exists(buildDir))
			throw new DirectoryNotFoundException("WebGL build folder not found: " + buildDir);
		if (!File.Exists(Path.Combine(buildDir, ScormManifestBuilder.LaunchFile)))
			throw new FileNotFoundException("The WebGL build has no " + ScormManifestBuilder.LaunchFile + ": " + buildDir);
		List<string> errors = settings.Validate();
		if (errors.Count > 0)
			throw new ArgumentException("Invalid SCORM settings:\n" + string.Join("\n", errors.ToArray()));

		ScormPackageResult result = new ScormPackageResult();
		string warning;
		result.xsdDirectory = ResolveXsdDirectory(xsdRoot, settings.edition, out warning);
		if (warning != null)
			result.warnings.Add(warning);
		result.warnings.AddRange(ScormManifestBuilder.GetEditionWarnings(settings));
		if (!Directory.Exists(result.xsdDirectory))
			throw new DirectoryNotFoundException("XSD folder not found: " + result.xsdDirectory);

		List<string> content = ListContentFiles(buildDir);
		List<string> xsdFiles = ListContentFiles(result.xsdDirectory);
		result.contentFileCount = content.Count;
		string manifest = ScormManifestBuilder.Build(settings, content);

		string zipFull = Path.GetFullPath(zipPath);
		string zipDir = Path.GetDirectoryName(zipFull);
		if (!string.IsNullOrEmpty(zipDir))
			Directory.CreateDirectory(zipDir);
		if (File.Exists(zipFull))
			File.Delete(zipFull);

		HashSet<string> entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		using (FileStream fs = new FileStream(zipFull, FileMode.CreateNew))
		using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create)) {
			ZipArchiveEntry manifestEntry = zip.CreateEntry(ScormManifestBuilder.ManifestFileName, CompressionLevel.Optimal);
			using (Stream s = manifestEntry.Open()) {
				byte[] bytes = new UTF8Encoding(false).GetBytes(manifest);
				s.Write(bytes, 0, bytes.Length);
			}
			entries.Add(ScormManifestBuilder.ManifestFileName);

			foreach (string relative in content)
				AddFile(zip, Path.Combine(buildDir, relative), relative, entries);

			foreach (string relative in xsdFiles) {
				if (entries.Contains(relative)) {
					result.warnings.Add("XSD file skipped because the build already contains " + relative);
					continue;
				}
				AddFile(zip, Path.Combine(result.xsdDirectory, relative), relative, entries);
			}
		}

		foreach (string entry in entries)
			if (entry.IndexOf('\\') >= 0)
				throw new InvalidOperationException("Zip entry with '\\' separator: " + entry);

		result.zipPath = zipFull;
		return result;
	}

	static void AddFile(ZipArchive zip, string sourcePath, string entryName, HashSet<string> entries) {
		entryName = entryName.Replace('\\', '/');
		// .unityweb/.gz/.br/.wasm/.png are already compressed; storing them avoids wasting time.
		string ext = Path.GetExtension(entryName).ToLowerInvariant();
		CompressionLevel level = ext == ".unityweb" || ext == ".gz" || ext == ".br" || ext == ".png" || ext == ".ico" ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
		ZipArchiveEntry entry = zip.CreateEntry(entryName, level);
		entry.LastWriteTime = File.GetLastWriteTime(sourcePath);
		using (Stream target = entry.Open())
		using (FileStream source = File.OpenRead(sourcePath))
			source.CopyTo(target);
		entries.Add(entryName);
	}

	/// <summary>
	/// Validates a manifest against the XSD in xsdDir (imscp, adlcp, adlseq, adlnav, imsss and lom when present).
	/// Schema warnings such as "could not find schema information" are reported as errors.
	/// </summary>
	public static List<string> ValidateManifest(string manifestPath, string xsdDir) {
		List<string> errors = new List<string>();
		XmlUrlResolver resolver = new XmlUrlResolver();

		XmlReaderSettings schemaReaderSettings = new XmlReaderSettings();
		schemaReaderSettings.DtdProcessing = DtdProcessing.Parse;
		schemaReaderSettings.XmlResolver = resolver;

		XmlSchemaSet schemas = new XmlSchemaSet();
		schemas.XmlResolver = resolver;
		// Compile-time warnings of the ADL XSD themselves (e.g. "Empty choice cannot be satisfied..." in the LOM
		// schemas) are not problems of the manifest; only schema errors are reported here.
		schemas.ValidationEventHandler += (sender, e) => {
			if (e.Severity == XmlSeverityType.Error)
				errors.Add("[schema] " + e.Severity + ": " + e.Message);
		};
		string[] roots = { "imscp_v1p1.xsd", "adlcp_v1p3.xsd", "adlseq_v1p3.xsd", "adlnav_v1p3.xsd", "imsss_v1p0.xsd", "lom.xsd" };
		foreach (string name in roots) {
			string path = Path.Combine(xsdDir, name);
			if (!File.Exists(path)) {
				if (name != "lom.xsd")
					errors.Add("[schema] missing " + path);
				continue;
			}
			try {
				using (XmlReader r = XmlReader.Create(new Uri(Path.GetFullPath(path)).AbsoluteUri, schemaReaderSettings))
					schemas.Add(null, r);
			} catch (Exception e) {
				errors.Add("[schema] " + name + ": " + e.Message);
			}
		}
		try {
			schemas.Compile();
		} catch (Exception e) {
			errors.Add("[schema] compile: " + e.Message);
		}
		if (errors.Count > 0)
			return errors;

		XmlReaderSettings settings = new XmlReaderSettings();
		settings.ValidationType = ValidationType.Schema;
		settings.Schemas = schemas;
		settings.DtdProcessing = DtdProcessing.Parse;
		settings.XmlResolver = resolver;
		settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
		settings.ValidationEventHandler += (sender, e) =>
			errors.Add("[manifest] " + e.Severity + " (line " + e.Exception.LineNumber + "): " + e.Message);
		try {
			using (XmlReader reader = XmlReader.Create(new Uri(Path.GetFullPath(manifestPath)).AbsoluteUri, settings))
				while (reader.Read()) { }
		} catch (Exception e) {
			errors.Add("[manifest] " + e.Message);
		}
		return errors;
	}

	/// <summary>
	/// Extracts zipPath to a temporary folder and checks: imsmanifest.xml at the root, no "\" or .meta entries,
	/// every &lt;file href&gt; present, and the manifest valid against the XSD shipped in the zip.
	/// </summary>
	public static List<string> ValidateZip(string zipPath, out string report) {
		List<string> errors = new List<string>();
		StringBuilder log = new StringBuilder();
		string temp = Path.Combine(Path.GetTempPath(), "scorm_validate_" + Guid.NewGuid().ToString("N"));
		try {
			HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
			using (ZipArchive zip = ZipFile.OpenRead(zipPath)) {
				foreach (ZipArchiveEntry e in zip.Entries) {
					names.Add(e.FullName);
					if (e.FullName.IndexOf('\\') >= 0) errors.Add("Entry uses '\\': " + e.FullName);
					if (e.FullName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) errors.Add(".meta entry: " + e.FullName);
				}
				log.AppendLine("Entries: " + zip.Entries.Count);
				zip.ExtractToDirectory(temp);
			}
			if (!names.Contains(ScormManifestBuilder.ManifestFileName)) {
				errors.Add("imsmanifest.xml is not at the zip root");
				report = log.ToString();
				return errors;
			}

			string manifestPath = Path.Combine(temp, ScormManifestBuilder.ManifestFileName);
			XmlDocument doc = new XmlDocument();
			doc.XmlResolver = null;
			doc.Load(manifestPath);
			XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
			ns.AddNamespace("cp", "http://www.imsglobal.org/xsd/imscp_v1p1");
			XmlNodeList files = doc.SelectNodes("//cp:resource/cp:file/@href", ns);
			int missing = 0;
			foreach (XmlNode href in files) {
				string path = Uri.UnescapeDataString(href.Value);
				if (!names.Contains(path)) { errors.Add("<file href> not in zip: " + href.Value); missing++; }
			}
			XmlNode launch = doc.SelectSingleNode("//cp:resource/@href", ns);
			if (launch == null || !names.Contains(launch.Value)) errors.Add("Launch file (resource href) not in zip");
			XmlNode version = doc.SelectSingleNode("//cp:metadata/cp:schemaversion", ns);
			log.AppendLine("schemaversion: " + (version != null ? version.InnerText : "(none)"));
			log.AppendLine("<file> elements: " + files.Count + " (missing: " + missing + ")");

			List<string> schemaErrors = ValidateManifest(manifestPath, temp);
			log.AppendLine("XSD validation errors/warnings: " + schemaErrors.Count);
			errors.AddRange(schemaErrors);
		} catch (Exception e) {
			errors.Add("ValidateZip: " + e.Message);
		} finally {
			try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch (Exception) { }
		}
		report = log.ToString();
		return errors;
	}
}
