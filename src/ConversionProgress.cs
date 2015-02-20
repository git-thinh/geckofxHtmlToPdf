﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Gecko;
using System.Linq;
using System.Configuration;

namespace GeckofxHtmlToPdf
{
	/// <summary>
	/// This class is used when the exe is called from the command line. It is invisibe
	/// if the --quiet parameter is used, otherwise it gives a little progress dialog.
	/// </summary>
	public partial class ConversionProgress : Form
	{
		private readonly ConversionOrder _conversionOrder;

		public ConversionProgress(ConversionOrder conversionOrder)
		{
			_conversionOrder = conversionOrder;
			InitializeComponent();
			_progressBar.Maximum = 100;
			if (conversionOrder.NoUIMode)
			{
				this.WindowState = FormWindowState.Minimized;
				this.ShowInTaskbar = false;
			}

			SetUpXulRunner();
		}

		private void ConversionProgress_Load(object sender, EventArgs e)
		{
			_pdfMaker.Start(_conversionOrder);
		}

		void OnPdfMaker_Finished(object sender, EventArgs e)
		{
			_statusLabel.Text = "Finished";
			
			//on windows 7 (at least) you won't see 100% if you close before the system has had a chance to "animate" the increase. 
			//On very short documents, you won't see it get past around 20%. Now good. So, the
			//trick here is to go *down* to 99, that going downwards makes it skip the animation delay.
			_progressBar.Value = 100;
			_progressBar.Value = 99;		
			Close();
		}

		private void OnPdfMaker_StatusChanged(object sender, PdfMakingStatus pdfMakingStatus)
		{
			_statusLabel.Text = pdfMakingStatus.statusLabel;
			_progressBar.Value = pdfMakingStatus.percentage;
		}

		#region FindingXulRunner
		/// <summary>
		/// Gives the directory of either the project folder (if running from visual studio), or
		/// the installation folder.  Helpful for finding templates and things; by using this,
		/// you don't have to copy those files into the build directory during development.
		/// It assumes your build directory has "output" as part of its path.
		/// </summary>
		/// <returns></returns>
		public static string DirectoryOfApplicationOrSolution
		{
			get
			{
				string path = DirectoryOfTheApplicationExecutable;
				char sep = Path.DirectorySeparatorChar;
				int i = path.ToLower().LastIndexOf(sep + "output" + sep);

				if (i > -1)
				{
					path = path.Substring(0, i + 1);
				}
				return path;
			}
		}

		public static string DirectoryOfTheApplicationExecutable
		{
			get
			{
				string path;
				bool unitTesting = Assembly.GetEntryAssembly() == null;
				if (unitTesting)
				{
					path = new Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath;
					path = Uri.UnescapeDataString(path);
				}
				else
				{
					var assembly = Assembly.GetEntryAssembly();
					path = assembly.Location;
				}
				return Directory.GetParent(path).FullName;
			}
		}

		/// <summary>
		/// Initialize Xpcom so that we can access xulrunner via Geckofx.
		/// </summary>
		/// <remarks>
		/// This logic is a merge of the original logic in the program (which looked
		/// in distfiles) and the logic in BloomExe/Browser.cs (which looks at the
		/// environment variable XULRUNNER and later in lib).  It would be nice to
		/// a common place where this code could exist once, but that's left for
		/// another day.  One of the palaso libraries might be a good place, but
		/// it demands indirect access to Xpcom via reflection.
		/// </remarks>
		public static void SetUpXulRunner()
		{
			if (Xpcom.IsInitialized)
				return;

			string xulRunnerPath = Environment.GetEnvironmentVariable("XULRUNNER");
			if (String.IsNullOrEmpty(xulRunnerPath) || !Directory.Exists(xulRunnerPath))
			{
				xulRunnerPath = Path.Combine(DirectoryOfApplicationOrSolution, "xulrunner");
				if (!Directory.Exists(xulRunnerPath))
				{
					//if this is a programmer, go look in the lib directory
					xulRunnerPath = Path.Combine(DirectoryOfApplicationOrSolution,
						Path.Combine("lib", "xulrunner"));
				}
				//on my build machine, I really like to have the dir labelled with the version.
				//but it's a hassle to update all the other parts (installer, build machine) with this number,
				//so we only use it if we don't find the unnumbered alternative.
				if (!Directory.Exists(xulRunnerPath))
				{
					xulRunnerPath = Path.Combine(DirectoryOfApplicationOrSolution,
						Path.Combine("lib", "xulrunner" + XulRunnerVersion));
				}
				if (!Directory.Exists(xulRunnerPath))
				{
					//or, go look in the distfiles directory
					xulRunnerPath = Path.Combine(DirectoryOfApplicationOrSolution,
						Path.Combine("distfiles", "xulrunner"));
				}
				if (!Directory.Exists(xulRunnerPath))
				{
					xulRunnerPath = Path.Combine(DirectoryOfApplicationOrSolution,
						Path.Combine("distfiles", "xulrunner" + XulRunnerVersion));
				}
				if (!Directory.Exists(xulRunnerPath))
				{
					throw new Exception(String.Format("Can't find the directory where xulrunner (version {0}) is installed", XulRunnerVersion));
				}
			}

			Xpcom.Initialize(xulRunnerPath);

			Application.ApplicationExit += OnApplicationExit;
		}

		private static void OnApplicationExit(object sender, EventArgs e)
		{
			// We come here iff we initialized Xpcom. In that case we want to call shutdown,
			// otherwise the app might not exit properly.
			if (Xpcom.IsInitialized)
				Xpcom.Shutdown();
			Application.ApplicationExit -= OnApplicationExit;
		}

		private static int XulRunnerVersion
		{
			get
			{
				var geckofx = Assembly.GetAssembly(typeof(GeckoWebBrowser));
				if (geckofx == null)
					return 0;

				var versionAttribute = geckofx.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true)
					.FirstOrDefault() as AssemblyFileVersionAttribute;
				return versionAttribute == null ? 0 : new Version(versionAttribute.Version).Major;
			}
		}
		#endregion
	}
}
