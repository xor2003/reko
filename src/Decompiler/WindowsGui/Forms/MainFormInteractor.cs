/* 
 * Copyright (C) 1999-2007 John K�ll�n.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using Decompiler.Core;
using Decompiler.Core.Serialization;
using Decompiler.Gui;
using Decompiler.WindowsGui.Controls;
using System;
using System.IO;
using System.Windows.Forms;

namespace Decompiler.WindowsGui.Forms
{
	/// <summary>
	/// Handles interaction with the MainForm, in order to decouple platform-
	/// specific code from the user interaction code. This will make it easier to port
	/// to other GUI platforms.
	/// </summary>
	public class MainFormInteractor : 
		ICommandTarget,
		DecompilerHost
	{
		private MainForm form;			//$REVIEW: in the future, this should be an interface.
		private DecompilerDriver decompiler;
		private DecompilerPhase initialPhase;
		private DecompilerPhase phase;
		private InitialPageInteractor pageInitial;
		private LoadedPageInteractor pageLoaded;
		private ICommandTarget cmdTarget;
		private MruList mru;

		private static string dirSettings;
		
		private const int MaxMruItems = 10;

		public MainFormInteractor()
		{
			mru = new MruList(MaxMruItems);
			mru.Load(MruListFile);
		}

		public MainFormInteractor(MainForm form) : this()
		{
			Attach(form);
		}

		public void Attach(MainForm form)
		{
			this.form = form;

			DecompilerMenus dm = new DecompilerMenus(this);
			form.Menu = dm.MainMenu;

			AttachInteractors();
			form.Closed += new System.EventHandler(this.MainForm_Closed);
			form.PhasePageChanged += new System.EventHandler(this.MainForm_PhasePageChanged);
			form.BrowserTree.AfterSelect += new TreeViewEventHandler(OnBrowserItemSelected);

			form.PhasePage = form.InitialPage;
		}

		private void AttachInteractors()
		{
			pageInitial = new InitialPageInteractor(form.InitialPage, this);
			pageLoaded = new LoadedPageInteractor(form.LoadedPage, this);
		}

		public virtual DecompilerDriver CreateDecompiler(string file)
		{
			return new DecompilerDriver(file, new Program(), this);
		}

		public MainForm MainForm
		{
			get { return form; }
		}

		public void OpenBinary(string file)
		{
			decompiler = CreateDecompiler(file);
			try
			{
				decompiler.LoadProgram();
				form.PhasePage = form.LoadedPage;
			} 	
			catch (Exception e)
			{
				form.AddDiagnostic(Diagnostic.FatalError, "Fatal error: {0}", e.Message);
				form.SetStatus("Terminated due to fatal error.");
			}
		}

		public void OpenBinaryWithPrompt()
		{
			Cursor.Current = Cursors.WaitCursor;
			try
			{
				if (form.OpenFileDialog.ShowDialog(form) == DialogResult.OK)
				{
					OpenBinary(form.OpenFileDialog.FileName);
					mru.Use(form.OpenFileDialog.FileName);
				}
			} 
			finally 
			{
				Cursor.Current = Cursors.Arrow;
				form.SetStatus("");
			}
		}


		public Program Program
		{
			get { return decompiler.Program; }
		}

		/// <summary>
		/// The current command target, which gets first chance at handling commands 
		/// before the MainFormInteractor does.
		/// </summary>
		public ICommandTarget CommandTarget
		{
			get { return cmdTarget; }
			set { cmdTarget = value; }
		}

		public void FinishDecompilation()
		{
			phase.Execute(decompiler);
			while (phase.NextPhase != null)
			{
				phase = phase.NextPhase;
				phase.Execute(decompiler);
			}
			form.ShowPhasePage(phase.Page, decompiler);

		}

		public InitialPageInteractor InitialPageInteractor
		{
			get { return pageInitial; }
		}

		public LoadedPageInteractor LoadedPageInteractor
		{
			get { return pageLoaded; }
		}

		private static string MruListFile
		{
			get { return SettingsDirectory + "\\mru.txt"; }
		}


		public void NextPhase()
		{
			phase.Execute(decompiler);
			if (phase.NextPhase != null)
			{
				phase = phase.NextPhase;
				form.ShowPhasePage(phase.Page, decompiler);
			}
		}

		private static string SettingsDirectory
		{
			get 
			{ 
				if (dirSettings == null)
				{
					string dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\jkl\\grovel"; 
					if (!Directory.Exists(dir))
						Directory.CreateDirectory(dir);
					dirSettings = dir;
				}
				return dirSettings;
			}
		}

		#region ICommandTarget members 
		public bool QueryStatus(ref Guid cmdSet, int cmdId, CommandStatus cmdStatus, CommandText cmdText)
		{
			if (CommandTarget != null && CommandTarget.QueryStatus(ref cmdSet, cmdId, cmdStatus, cmdText))
				return true;
			if (cmdSet == CmdSets.GuidDecompiler)
			{
				int iMru = cmdId - CmdIds.FileMru;
				if (0 <= iMru && iMru < mru.Items.Count)
				{
					cmdStatus.Status = MenuStatus.Visible|MenuStatus.Enabled;
					cmdText.Text = (string) mru.Items[iMru];
					return true;
				}

				switch (cmdId)
				{
				case CmdIds.FileOpen:
				case CmdIds.FileExit:
					cmdStatus.Status = MenuStatus.Enabled|MenuStatus.Visible;
					return true;
				case CmdIds.FileMru:
					cmdStatus.Status = MenuStatus.Visible;
					return true;
				}
			}
			return false;
		}

		public bool Execute(ref Guid cmdSet, int cmdId)
		{
			if (CommandTarget != null && CommandTarget.Execute(ref cmdSet, cmdId))
				return true;
			if (cmdSet == CmdSets.GuidDecompiler)
			{
				int iMru = cmdId - CmdIds.FileMru;
				if (0 <= iMru && iMru < mru.Items.Count)
				{
					string file = (string) mru.Items[iMru];
					OpenBinary(file);
					mru.Use(file);
					return true;
				}
				switch (cmdId)
				{
				case CmdIds.FileOpen: OpenBinaryWithPrompt(); return true;
				case CmdIds.FileExit: form.Close(); return true;
				}
			}
			return false;
		}

		#endregion

		#region DecompilerHost Members //////////////////////////////////

		public TextWriter DisassemblyWriter
		{
			get
			{
				// TODO:  Add GuiHost.DisassemblyWriter getter implementation
				return null;
			}
		}

		public System.IO.TextWriter TypesWriter
		{
			get
			{
				// TODO:  Add GuiHost.TypesWriter getter implementation
				return null;
			}
		}

		public void ShowProgress(string caption, int numerator, int denominator)
		{
			// TODO:  Add GuiHost.ShowProgress implementation
		}

		public void CodeStructuringComplete()
		{
		}

		public void DecompilationFinished()
		{
			form.SetStatus("Finished");
		}

		public void InterproceduralAnalysisComplete()
		{
		}

		public void MachineCodeRewritten()
		{
			form.SetStatus("Machine code rewritten.");
		}

		public void ProceduresTransformed()
		{
			form.SetStatus("Procedures transformed.");
		}

		public void ProgramLoaded()
		{
			form.SetStatus("Program loaded.");
		}

		public void ProgramScanned()
		{
			form.SetStatus("Program scanned.");
		}

		public void TypeReconstructionComplete()
		{
			form.SetStatus("Data types reconstructed.");
		}

		public void WriteDiagnostic(Diagnostic d, string format, params object[] args)
		{
			form.AddDiagnostic(d, format, args);
		}

		public System.IO.TextWriter DecompiledCodeWriter
		{
			get
			{
				// TODO:  Add GuiHost.DecompiledCodeWriter getter implementation
				return null;
			}
		}

		public System.IO.TextWriter IntermediateCodeWriter
		{
			get
			{
				// TODO:  Add GuiHost.IntermediateCodeWriter getter implementation
				return null;
			}
		}

		#endregion ////////////////////////////////////////////////////


		// Event handlers //////////////////////////////

		private void miFileExit_Click(object sender, System.EventArgs e)
		{
			form.Close();
		}

		private void MainForm_Closed(object sender, System.EventArgs e)
		{
			mru.Save(MruListFile);
		}


		private void statusBar_PanelClick(object sender, System.Windows.Forms.StatusBarPanelClickEventArgs e)
		{
		
		}

		private void txtLog_TextChanged(object sender, System.EventArgs e)
		{
		
		}


		private void toolBar_ButtonClick(object sender, System.Windows.Forms.ToolBarButtonClickEventArgs e)
		{
			/*
			//$REVIEW: this hard-wiring should use command routing instead. Store the menu commands in the tags of the 
			// toolbar.
			if (e.Button == tbtnNextPhase)
			{
				NextPhase();
			} 
			else if (e.Button == tbtnFinishDecompilation)
			{
				FinishDecompilation();
			}
			*/
		}

		private void MainForm_PhasePageChanged(object sender, EventArgs e)
		{
		}

		public void OnBrowserItemSelected(object sender, TreeViewEventArgs e)
		{
			Execute(ref CmdSets.GuidDecompiler, CmdIds.BrowserItemSelected);
		}
	}
}