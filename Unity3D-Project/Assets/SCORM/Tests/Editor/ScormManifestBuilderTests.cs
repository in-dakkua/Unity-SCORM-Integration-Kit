using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml;
using NUnit.Framework;
using UnityEngine;

/// <summary>imsmanifest.xml generation, XSD validation and zip packaging.</summary>
public class ScormManifestBuilderTests {

	const string Cp = "http://www.imsglobal.org/xsd/imscp_v1p1";
	const string Adlcp = "http://www.adlnet.org/xsd/adlcp_v1p3";

	static readonly string[] SampleFiles = { "index.html", "Build/WebGL.loader.js", "Build/WebGL.data.unityweb", "TemplateData/style.css", "TemplateData/scorm.js" };

	CultureInfo _previousCulture;
	readonly List<string> _tempPaths = new List<string>();

	static string Xsd4thDir { get { return Path.Combine(Application.dataPath, "SCORM/Plugins/2004_4th"); } }

	[SetUp]
	public void SetUp() {
		_previousCulture = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
	}

	[TearDown]
	public void TearDown() {
		Thread.CurrentThread.CurrentCulture = _previousCulture;
		foreach (string p in _tempPaths) {
			try {
				if (Directory.Exists(p)) Directory.Delete(p, true);
				else if (File.Exists(p)) File.Delete(p);
			} catch (Exception) { }
		}
	}

	string TempDir() {
		string dir = Path.Combine(Path.GetTempPath(), "scorm_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		_tempPaths.Add(dir);
		return dir;
	}

	static ScormPackageSettings Settings(ScormEdition edition) {
		ScormPackageSettings s = new ScormPackageSettings();
		s.edition = edition;
		s.identifier = "com.invelon.scormtestapp";
		s.courseTitle = "SCORM Test App";
		s.scoTitle = "Test App";
		return s;
	}

	static XmlDocument Load(string xml, out XmlNamespaceManager ns) {
		XmlDocument doc = new XmlDocument();
		doc.LoadXml(xml);
		ns = new XmlNamespaceManager(doc.NameTable);
		ns.AddNamespace("cp", Cp);
		ns.AddNamespace("adlcp", Adlcp);
		ns.AddNamespace("imsss", "http://www.imsglobal.org/xsd/imsss");
		return doc;
	}

	List<string> ValidateXml(string xml, string xsdDir) {
		string dir = TempDir();
		string path = Path.Combine(dir, "imsmanifest.xml");
		File.WriteAllText(path, xml);
		return ScormPackager.ValidateManifest(path, xsdDir);
	}

	[Test]
	public void Build_3rdEdition_WritesSchemaVersionAndDecimalCompletionThreshold() {
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_3rd);
		s.completionThreshold = 0.8f;
		XmlNamespaceManager ns;
		XmlDocument doc = Load(ScormManifestBuilder.Build(s, SampleFiles), out ns);
		Assert.AreEqual("2004 3rd Edition", doc.SelectSingleNode("//cp:metadata/cp:schemaversion", ns).InnerText);
		XmlNode ct = doc.SelectSingleNode("//cp:item/adlcp:completionThreshold", ns);
		Assert.IsNotNull(ct);
		Assert.AreEqual("0.8", ct.InnerText);
		Assert.AreEqual(0, ct.Attributes.Count);
	}

	[Test]
	public void Build_4thEdition_WritesCompletionThresholdAttributes() {
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_4th);
		s.completedByMeasure = true;
		s.minProgressMeasure = 0.8f;
		XmlNamespaceManager ns;
		XmlDocument doc = Load(ScormManifestBuilder.Build(s, SampleFiles), out ns);
		Assert.AreEqual("2004 4th Edition", doc.SelectSingleNode("//cp:metadata/cp:schemaversion", ns).InnerText);
		XmlElement ct = (XmlElement)doc.SelectSingleNode("//cp:item/adlcp:completionThreshold", ns);
		Assert.IsNotNull(ct);
		Assert.AreEqual("true", ct.GetAttribute("completedByMeasure"));
		Assert.AreEqual("0.8", ct.GetAttribute("minProgressMeasure"));
		Assert.AreEqual("", ct.InnerText);
	}

	[Test]
	public void Build_4thEdition_Maps3rdCompletionThresholdWithoutCompletedByMeasureAndWarns() {
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_4th);
		s.completionThreshold = 0.8f;
		string xml = ScormManifestBuilder.Build(s, SampleFiles);
		XmlNamespaceManager ns;
		XmlElement ct = (XmlElement)Load(xml, out ns).SelectSingleNode("//cp:item/adlcp:completionThreshold", ns);
		Assert.IsNotNull(ct);
		Assert.AreEqual("false", ct.GetAttribute("completedByMeasure"));
		Assert.AreEqual("0.8", ct.GetAttribute("minProgressMeasure"));
		Assert.AreEqual("", ct.InnerText);
		List<string> warnings = ScormManifestBuilder.GetEditionWarnings(s);
		Assert.AreEqual(1, warnings.Count);
		StringAssert.Contains("3rd Edition setting", warnings[0]);
		CollectionAssert.IsEmpty(ValidateXml(xml, Xsd4thDir));
	}

	[Test]
	public void Build_3rdEdition_Maps4thCompletedByMeasureToElementAndWarns() {
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_3rd);
		s.completedByMeasure = true;
		s.minProgressMeasure = 0.6f;
		string xml = ScormManifestBuilder.Build(s, SampleFiles);
		XmlNamespaceManager ns;
		XmlNode ct = Load(xml, out ns).SelectSingleNode("//cp:item/adlcp:completionThreshold", ns);
		Assert.IsNotNull(ct);
		Assert.AreEqual("0.6", ct.InnerText);
		Assert.AreEqual(0, ct.Attributes.Count);
		List<string> warnings = ScormManifestBuilder.GetEditionWarnings(s);
		Assert.AreEqual(1, warnings.Count);
		StringAssert.Contains("4th Edition settings", warnings[0]);
		CollectionAssert.IsEmpty(ValidateXml(xml, Xsd4thDir));
	}

	[Test]
	public void Build_BothEditionsSettings_UsesOwnEditionAndWarnsAboutTheOther() {
		ScormPackageSettings s4 = Settings(ScormEdition.Scorm2004_4th);
		s4.completionThreshold = 0.3f;
		s4.completedByMeasure = true;
		s4.minProgressMeasure = 0.9f;
		XmlNamespaceManager ns;
		XmlElement ct = (XmlElement)Load(ScormManifestBuilder.Build(s4, SampleFiles), out ns).SelectSingleNode("//cp:item/adlcp:completionThreshold", ns);
		Assert.AreEqual("true", ct.GetAttribute("completedByMeasure"));
		Assert.AreEqual("0.9", ct.GetAttribute("minProgressMeasure"));
		StringAssert.Contains("ignored", ScormManifestBuilder.GetEditionWarnings(s4).Single());

		ScormPackageSettings s3 = Settings(ScormEdition.Scorm2004_3rd);
		s3.completionThreshold = 0.3f;
		s3.completedByMeasure = true;
		s3.minProgressMeasure = 0.9f;
		Assert.AreEqual("0.3", Load(ScormManifestBuilder.Build(s3, SampleFiles), out ns).SelectSingleNode("//cp:item/adlcp:completionThreshold", ns).InnerText);
		StringAssert.Contains("ignored", ScormManifestBuilder.GetEditionWarnings(s3).Single());
	}

	[Test]
	public void GetEditionWarnings_OwnEditionSettings_NoWarnings() {
		ScormPackageSettings s3 = Settings(ScormEdition.Scorm2004_3rd);
		s3.completionThreshold = 0.8f;
		ScormPackageSettings s4 = Settings(ScormEdition.Scorm2004_4th);
		s4.completedByMeasure = true;
		CollectionAssert.IsEmpty(ScormManifestBuilder.GetEditionWarnings(s3));
		CollectionAssert.IsEmpty(ScormManifestBuilder.GetEditionWarnings(s4));
		CollectionAssert.IsEmpty(ScormManifestBuilder.GetEditionWarnings(Settings(ScormEdition.Scorm2004_4th)));
	}

	[Test]
	public void Package_AddsEditionWarningsToResult() {
		string build = TempDir();
		File.WriteAllText(Path.Combine(build, "index.html"), "<html></html>");
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_4th);
		s.completionThreshold = 0.8f;
		ScormPackageResult result = ScormPackager.Package(build, Path.Combine(TempDir(), "out.zip"), s, Path.Combine(Application.dataPath, "SCORM/Plugins"));
		Assert.IsTrue(result.warnings.Any(w => w.Contains("minProgressMeasure=\"0.8\"")), string.Join("\n", result.warnings.ToArray()));
	}

	[Test]
	public void Build_WithoutOptionalValues_OmitsOptionalElements() {
		XmlNamespaceManager ns;
		XmlDocument doc = Load(ScormManifestBuilder.Build(Settings(ScormEdition.Scorm2004_3rd), SampleFiles), out ns);
		Assert.IsNull(doc.SelectSingleNode("//imsss:sequencing", ns));
		Assert.IsNull(doc.SelectSingleNode("//adlcp:timeLimitAction", ns));
		Assert.IsNull(doc.SelectSingleNode("//adlcp:dataFromLMS", ns));
		Assert.IsNull(doc.SelectSingleNode("//adlcp:completionThreshold", ns));
	}

	[Test]
	public void Build_WithTimeLimit_WritesLimitConditionsAndTimeLimitAction() {
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_3rd);
		s.timeLimitSecs = 3600.5f;
		s.timeLimitAction = "exit,message";
		s.launchData = "level=1";
		XmlNamespaceManager ns;
		XmlDocument doc = Load(ScormManifestBuilder.Build(s, SampleFiles), out ns);
		XmlElement limit = (XmlElement)doc.SelectSingleNode("//cp:item/imsss:sequencing/imsss:limitConditions", ns);
		Assert.AreEqual("P0DT1H0M0.5S", limit.GetAttribute("attemptAbsoluteDurationLimit"));
		Assert.AreEqual("exit,message", doc.SelectSingleNode("//adlcp:timeLimitAction", ns).InnerText);
		Assert.AreEqual("level=1", doc.SelectSingleNode("//adlcp:dataFromLMS", ns).InnerText);
	}

	[Test]
	public void Build_EscapesSpecialCharacters() {
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_3rd);
		s.courseTitle = "Tom & Jerry <\"quoted\">";
		s.courseDescription = "a < b & c";
		string xml = ScormManifestBuilder.Build(s, SampleFiles);
		XmlNamespaceManager ns;
		XmlDocument doc = Load(xml, out ns);
		Assert.AreEqual("Tom & Jerry <\"quoted\">", doc.SelectSingleNode("//cp:organization/cp:title", ns).InnerText);
		StringAssert.Contains("Tom &amp; Jerry &lt;", xml);
	}

	[Test]
	public void Build_FileHrefs_UseSlashesAreEscapedAndExcludeManifest() {
		string[] files = { "Build\\My Game.wasm.unityweb", "imsmanifest.xml", "index.html", "x.meta" };
		XmlNamespaceManager ns;
		XmlDocument doc = Load(ScormManifestBuilder.Build(Settings(ScormEdition.Scorm2004_3rd), files), out ns);
		List<string> hrefs = doc.SelectNodes("//cp:resource/cp:file/@href", ns).Cast<XmlNode>().Select(n => n.Value).ToList();
		CollectionAssert.AreEqual(new[] { "index.html", "Build/My%20Game.wasm.unityweb" }, hrefs);
		Assert.AreEqual("index.html", ((XmlElement)doc.SelectSingleNode("//cp:resource", ns)).GetAttribute("href"));
	}

	[Test]
	public void Build_HasNoBomAndIsUtf8() {
		string xml = ScormManifestBuilder.Build(Settings(ScormEdition.Scorm2004_3rd), SampleFiles);
		Assert.AreNotEqual('﻿', xml[0]);
		StringAssert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);
	}

	[Test]
	public void Build_3rdAnd4th_AreValidAgainst4thEditionXsd() {
		ScormPackageSettings s3 = Settings(ScormEdition.Scorm2004_3rd);
		s3.completionThreshold = 0.8f;
		s3.timeLimitSecs = 600f;
		s3.timeLimitAction = "continue,no message";
		s3.launchData = "data";
		s3.courseDescription = "Description & more";
		CollectionAssert.IsEmpty(ValidateXml(ScormManifestBuilder.Build(s3, SampleFiles), Xsd4thDir));

		ScormPackageSettings s4 = Settings(ScormEdition.Scorm2004_4th);
		s4.completedByMeasure = true;
		s4.minProgressMeasure = 0.5f;
		s4.timeLimitSecs = 600f;
		CollectionAssert.IsEmpty(ValidateXml(ScormManifestBuilder.Build(s4, SampleFiles), Xsd4thDir));
	}

	[Test]
	public void Build_3rd_IsValidAgainst3rdEditionXsd_WhenAvailable() {
		string dir3rd = Path.Combine(Application.dataPath, "SCORM/Plugins/2004_3rd");
		if (!Directory.Exists(dir3rd) || Directory.GetFiles(dir3rd, "*.xsd").Length == 0)
			Assert.Ignore("Plugins/2004_3rd has no XSD yet (approved temporary fallback to the 4th Edition set).");
		ScormPackageSettings s3 = Settings(ScormEdition.Scorm2004_3rd);
		s3.completionThreshold = 0.8f;
		CollectionAssert.IsEmpty(ValidateXml(ScormManifestBuilder.Build(s3, SampleFiles), dir3rd));
	}

	[Test]
	public void ValidateManifest_ReportsInvalidManifest() {
		// Negative control: proves the validator really loads the schemas.
		ScormPackageSettings s = Settings(ScormEdition.Scorm2004_3rd);
		string xml = ScormManifestBuilder.Build(s, SampleFiles)
			.Replace("adlcp:scormType=\"sco\"", "adlcp:scormType=\"bogus\"")
			.Replace("<title>Test App</title>", "<title>Test App</title><adlcp:timeLimitAction>never</adlcp:timeLimitAction>");
		List<string> errors = ValidateXml(xml, Xsd4thDir);
		Assert.GreaterOrEqual(errors.Count, 2, string.Join("\n", errors.ToArray()));
	}

	[Test]
	public void ValidateManifest_ReportsUnknownNamespaceElement() {
		string xml = ScormManifestBuilder.Build(Settings(ScormEdition.Scorm2004_3rd), SampleFiles)
			.Replace("<organizations", "<unknownElement /><organizations");
		CollectionAssert.IsNotEmpty(ValidateXml(xml, Xsd4thDir));
	}

	[Test]
	public void ResolveXsdDirectory_FallsBackTo4thWhen3rdIsMissingOrEmpty() {
		string root = TempDir();
		string warning;
		Assert.AreEqual(Path.Combine(root, "2004_4th"), ScormPackager.ResolveXsdDirectory(root, ScormEdition.Scorm2004_3rd, out warning));
		Assert.IsNotNull(warning);

		Directory.CreateDirectory(Path.Combine(root, "2004_3rd"));
		File.WriteAllText(Path.Combine(root, "2004_3rd", "readme.txt"), "x");
		Assert.AreEqual(Path.Combine(root, "2004_4th"), ScormPackager.ResolveXsdDirectory(root, ScormEdition.Scorm2004_3rd, out warning));
		Assert.IsNotNull(warning);

		File.WriteAllText(Path.Combine(root, "2004_3rd", "imscp_v1p1.xsd"), "<x/>");
		Assert.AreEqual(Path.Combine(root, "2004_3rd"), ScormPackager.ResolveXsdDirectory(root, ScormEdition.Scorm2004_3rd, out warning));
		Assert.IsNull(warning);

		Assert.AreEqual(Path.Combine(root, "2004_4th"), ScormPackager.ResolveXsdDirectory(root, ScormEdition.Scorm2004_4th, out warning));
		Assert.IsNull(warning);
	}

	[Test]
	public void Package_CreatesValidZipWithManifestAndXsdAtRoot() {
		string build = TempDir();
		Directory.CreateDirectory(Path.Combine(build, "Build"));
		Directory.CreateDirectory(Path.Combine(build, "TemplateData"));
		File.WriteAllText(Path.Combine(build, "index.html"), "<html></html>");
		File.WriteAllText(Path.Combine(build, "Build", "WebGL.loader.js"), "//");
		File.WriteAllText(Path.Combine(build, "TemplateData", "scorm.js"), "//");
		File.WriteAllText(Path.Combine(build, "TemplateData", "scorm.js.meta"), "meta");
		File.WriteAllText(Path.Combine(build, ".DS_Store"), "x");

		string zipPath = Path.Combine(TempDir(), "out.zip");
		ScormPackageResult result = ScormPackager.Package(build, zipPath, Settings(ScormEdition.Scorm2004_3rd), Path.Combine(Application.dataPath, "SCORM/Plugins"));
		Assert.AreEqual(3, result.contentFileCount);

		List<string> names;
		using (ZipArchive zip = ZipFile.OpenRead(zipPath))
			names = zip.Entries.Select(e => e.FullName).ToList();
		CollectionAssert.Contains(names, "imsmanifest.xml");
		CollectionAssert.Contains(names, "imscp_v1p1.xsd");
		CollectionAssert.Contains(names, "common/dataTypes.xsd");
		CollectionAssert.Contains(names, "Build/WebGL.loader.js");
		Assert.IsFalse(names.Any(n => n.Contains("\\")), "backslash entry");
		Assert.IsFalse(names.Any(n => n.EndsWith(".meta")), ".meta entry");
		Assert.IsFalse(names.Any(n => n.StartsWith(".")), "hidden entry");

		string report;
		List<string> errors = ScormPackager.ValidateZip(zipPath, out report);
		CollectionAssert.IsEmpty(errors, report + string.Join("\n", errors.ToArray()));
	}
}
