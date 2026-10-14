using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly:AssemblyTitle("Cross Rhythm")]
[assembly:AssemblyDescription("Cross Rhythm startup launcher")]
[assembly:AssemblyCompany("TakKa")]
[assembly:AssemblyProduct("Cross Rhythm")]
[assembly:AssemblyVersion("0.3.39.0")]
[assembly:AssemblyFileVersion("0.3.39.0")]
[assembly:AssemblyInformationalVersion("0.3.39")]

namespace CrossRhythmLauncher {
internal static class Program {
    const string MutexName=@"Local\TakKa.CrossRhythm.Launcher.v1";
    const string ActivateEventName=@"Local\TakKa.CrossRhythm.Launcher.Activate.v1";
    internal const string ReadyEventName=@"Local\TakKa.CrossRhythm.Launcher.StartupReady.v1";
    internal const string PlayerProcessName="CrossRhythmPlayer";

    [STAThread]
    static void Main(){
        bool first;
        using(var mutex=new Mutex(true,MutexName,out first))
        using(var activate=new EventWaitHandle(false,EventResetMode.AutoReset,ActivateEventName)){
            if(!first){activate.Set();return;}
            if(TryActivatePlayer())return;
            bool created;
            using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,ReadyEventName,out created)){
                ready.Reset();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using(var form=new LauncherForm(activate,ready,Environment.GetCommandLineArgs().Skip(1).ToArray()))Application.Run(form);
            }
        }
    }

    internal static bool TryActivatePlayer(){
        bool found=false;
        foreach(var process in Process.GetProcessesByName(PlayerProcessName)){
            try{
                found=true;process.Refresh();
                var window=process.MainWindowHandle;
                if(window==IntPtr.Zero)continue;
                Native.ShowWindow(window,Native.SW_RESTORE);
                Native.SetForegroundWindow(window);
                return true;
            }catch(Exception){}finally{process.Dispose();}
        }
        // Never start another Unity player while an earlier one is still creating
        // its native window. This keeps Unity's own multiple-instance alert hidden.
        return found;
    }

    internal static string QuoteArgument(string value){
        if(value.Length>0&&!value.Any(c=>char.IsWhiteSpace(c)||c=='"'))return value;
        var result=new StringBuilder("\"");
        int slashes=0;
        foreach(char c in value){
            if(c=='\\'){slashes++;continue;}
            if(c=='"'){result.Append('\\',slashes*2+1);result.Append(c);slashes=0;continue;}
            result.Append('\\',slashes);slashes=0;result.Append(c);
        }
        result.Append('\\',slashes*2);result.Append('"');
        return result.ToString();
    }
}

internal sealed class LauncherForm:Form {
    readonly EventWaitHandle activate,ready;
    readonly string[] arguments;
    readonly string statusPath;
    readonly Label title,heading,detail;
    readonly ProgressBar progress;
    readonly Button close;
    readonly System.Windows.Forms.Timer timer;
    Process player;
    bool startupReady,playerReady,cancelRequested,allowClose;
    DateTime cancelAt;

    internal LauncherForm(EventWaitHandle activateEvent,EventWaitHandle readyEvent,string[] playerArguments){
        activate=activateEvent;ready=readyEvent;arguments=RemoveReservedArguments(playerArguments);
        statusPath=Path.Combine(Path.GetTempPath(),"cross-rhythm-startup-"+Process.GetCurrentProcess().Id+".txt");
        Text="Cross Rhythm";StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.None;
        WindowState=FormWindowState.Normal;Bounds=Screen.FromPoint(Cursor.Position).Bounds;
        MaximizeBox=false;MinimizeBox=false;KeyPreview=true;TopMost=true;ShowInTaskbar=true;
        BackColor=Color.FromArgb(7,13,19);ForeColor=Color.FromArgb(239,247,249);
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);

        title=new Label{Text="CROSS RHYTHM",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,Font=new Font("Segoe UI Semibold",38,FontStyle.Bold),ForeColor=Color.FromArgb(132,232,196),BackColor=Color.Transparent};
        heading=new Label{Text=Japanese()?"Cross Rhythm を起動しています":"Starting Cross Rhythm",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,Font=new Font("Segoe UI Semibold",20,FontStyle.Bold),ForeColor=Color.White,BackColor=Color.Transparent};
        detail=new Label{Text=Japanese()?"画面を準備しています":"Preparing the game",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,Font=new Font("Segoe UI",12.5f),ForeColor=Color.FromArgb(199,216,223),BackColor=Color.Transparent};
        progress=new ProgressBar{Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=22};
        close=new Button{Text="Close",FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(20,34,43),ForeColor=Color.White,Font=new Font("Segoe UI",11),TabIndex=0,AccessibleName="Close startup"};
        close.FlatAppearance.BorderColor=Color.FromArgb(80,112,126);close.FlatAppearance.BorderSize=1;close.Click+=(s,e)=>RequestCancel();
        Controls.Add(title);Controls.Add(heading);Controls.Add(detail);Controls.Add(progress);Controls.Add(close);
        Resize+=(s,e)=>LayoutControls();Shown+=(s,e)=>{LayoutControls();StartPlayer();};
        KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){RequestCancel();e.Handled=true;e.SuppressKeyPress=true;}};
        FormClosing+=OnClosing;
        timer=new System.Windows.Forms.Timer{Interval=50};timer.Tick+=(s,e)=>Tick();timer.Start();
        LayoutControls();
    }

    static bool Japanese(){return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja",StringComparison.OrdinalIgnoreCase);}

    static string[] RemoveReservedArguments(string[] source){
        var result=new List<string>();
        for(int i=0;i<source.Length;i++){
            if((source[i]=="--startup-ready-event"||source[i]=="--startup-status-file")&&i+1<source.Length){i++;continue;}
            result.Add(source[i]);
        }
        return result.ToArray();
    }

    void LayoutControls(){
        int width=Math.Min(780,Math.Max(360,ClientSize.Width-80));
        int centerY=ClientSize.Height/2;
        title.Bounds=new Rectangle((ClientSize.Width-width)/2,centerY-150,width,72);
        heading.Bounds=new Rectangle((ClientSize.Width-width)/2,centerY-64,width,46);
        detail.Bounds=new Rectangle((ClientSize.Width-width)/2,centerY-12,width,34);
        progress.Bounds=new Rectangle((ClientSize.Width-width)/2,centerY+42,width,8);
        close.Bounds=new Rectangle(Math.Max(24,ClientSize.Width-152),24,120,42);
    }

    void StartPlayer(){
        string root=AppDomain.CurrentDomain.BaseDirectory;
        string runtime=Path.Combine(root,"Runtime"),path=Path.Combine(runtime,"CrossRhythmPlayer.exe");
        if(!File.Exists(path)){ShowFailure(Japanese()?"Runtime\\CrossRhythmPlayer.exe が見つかりません":"Runtime\\CrossRhythmPlayer.exe was not found");return;}
        try{
            try{if(File.Exists(statusPath))File.Delete(statusPath);}catch(Exception){}
            Icon=Icon.ExtractAssociatedIcon(path);
            var launchArguments=new List<string>(arguments){"--startup-ready-event",Program.ReadyEventName,"--startup-status-file",statusPath};
            var info=new ProcessStartInfo(path,String.Join(" ",launchArguments.Select(Program.QuoteArgument).ToArray())){UseShellExecute=false,WorkingDirectory=runtime};
            player=Process.Start(info);
        }catch(Exception ex){ShowFailure((Japanese()?"起動できません: ":"Unable to start: ")+ex.Message);}
    }

    void Tick(){
        if(activate.WaitOne(0)){if(playerReady)BringPlayerForward();else BringLauncherForward();}
        if(player==null)return;
        try{
            player.Refresh();
            if(player.HasExited){
                if(cancelRequested||playerReady){ExitLauncher();return;}
                ShowFailure((Japanese()?"ゲームを起動できませんでした (終了コード ":"The game could not start (exit code ")+player.ExitCode+")");return;
            }
            if(cancelRequested){if(DateTime.UtcNow-cancelAt>TimeSpan.FromSeconds(2))ExitLauncher();return;}
            ReadStartupStatus();
            if(!startupReady&&ready.WaitOne(0))startupReady=true;
            if(startupReady&&!playerReady){
                playerReady=true;progress.MarqueeAnimationSpeed=0;progress.Style=ProgressBarStyle.Continuous;progress.Value=100;
                BringPlayerForward();TopMost=false;ShowInTaskbar=false;Hide();
            }
        }catch(InvalidOperationException){ExitLauncher();}
    }

    void ReadStartupStatus(){
        try{
            if(!File.Exists(statusPath))return;
            string value=File.ReadAllText(statusPath,Encoding.UTF8).Trim();
            if(value.Length>0&&value!=detail.Text)detail.Text=value;
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }

    bool BringPlayerForward(){
        try{
            if(player!=null){player.Refresh();var window=player.MainWindowHandle;if(window!=IntPtr.Zero){Native.ShowWindow(window,Native.SW_RESTORE);Native.SetForegroundWindow(window);return true;}}
        }catch(Exception){}
        return Program.TryActivatePlayer();
    }

    void BringLauncherForward(){
        ShowInTaskbar=true;TopMost=true;Show();WindowState=FormWindowState.Normal;Bounds=Screen.FromPoint(Cursor.Position).Bounds;Activate();LayoutControls();
    }

    void RequestCancel(){
        if(cancelRequested)return;
        cancelRequested=true;cancelAt=DateTime.UtcNow;close.Enabled=false;
        heading.Text=Japanese()?"起動を中止しています":"Stopping startup";
        detail.Text=Japanese()?"終了処理を待っています":"Waiting for the game to close";
        progress.MarqueeAnimationSpeed=0;
        if(player==null){ExitLauncher();return;}
        try{if(!player.HasExited)player.Kill();}catch(Exception){ExitLauncher();}
    }

    void ShowFailure(string message){
        heading.Text=Japanese()?"起動できませんでした":"Unable to start";detail.Text=message;
        progress.MarqueeAnimationSpeed=0;progress.Style=ProgressBarStyle.Continuous;progress.Value=0;
        close.Enabled=true;allowClose=true;
    }

    void ExitLauncher(){allowClose=true;timer.Stop();Close();}

    void OnClosing(object sender,FormClosingEventArgs e){if(allowClose)return;e.Cancel=true;RequestCancel();}

    protected override void Dispose(bool disposing){
        if(disposing){timer.Dispose();if(player!=null)player.Dispose();try{if(File.Exists(statusPath))File.Delete(statusPath);}catch(Exception){}}
        base.Dispose(disposing);
    }
}

internal static class Native {
    internal const int SW_RESTORE=9;
    [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr window,int command);
}
}
