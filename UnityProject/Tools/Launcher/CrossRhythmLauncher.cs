using System;
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
[assembly:AssemblyVersion("0.3.38.0")]
[assembly:AssemblyFileVersion("0.3.38.0")]
[assembly:AssemblyInformationalVersion("0.3.38")]

namespace CrossRhythmLauncher {
internal static class Program {
    const string MutexName=@"Local\TakKa.CrossRhythm.Launcher.v1";
    const string ActivateEventName=@"Local\TakKa.CrossRhythm.Launcher.Activate.v1";
    internal const string PlayerProcessName="CrossRhythmPlayer";

    [STAThread]
    static void Main(){
        bool first;
        using(var mutex=new Mutex(true,MutexName,out first))
        using(var activate=new EventWaitHandle(false,EventResetMode.AutoReset,ActivateEventName)){
            if(!first){activate.Set();TryActivatePlayer();return;}
            if(TryActivatePlayer())return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new LauncherForm(activate,Environment.GetCommandLineArgs().Skip(1).ToArray()))Application.Run(form);
        }
    }

    internal static bool TryActivatePlayer(){
        foreach(var process in Process.GetProcessesByName(PlayerProcessName)){
            try{
                process.Refresh();
                var window=process.MainWindowHandle;
                if(window==IntPtr.Zero)continue;
                Native.ShowWindow(window,Native.SW_RESTORE);
                Native.SetForegroundWindow(window);
                return true;
            }catch(Exception){}finally{process.Dispose();}
        }
        return false;
    }

    internal static string QuoteArgument(string value){
        if(value.Length>0&&!value.Any(c=>char.IsWhiteSpace(c)||c=='\"'))return value;
        var result=new StringBuilder("\"");
        int slashes=0;
        foreach(char c in value){
            if(c=='\\'){slashes++;continue;}
            if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);slashes=0;continue;}
            result.Append('\\',slashes);slashes=0;result.Append(c);
        }
        result.Append('\\',slashes*2);result.Append('\"');
        return result.ToString();
    }
}

internal sealed class LauncherForm:Form {
    readonly EventWaitHandle activate;
    readonly string[] arguments;
    readonly Label detail;
    readonly ProgressBar progress;
    readonly Button close;
    readonly System.Windows.Forms.Timer timer;
    Process player;
    bool playerReady,cancelRequested,allowClose;
    DateTime startedAt,cancelAt;

    internal LauncherForm(EventWaitHandle activateEvent,string[] playerArguments){
        activate=activateEvent;arguments=playerArguments;
        Text="Cross Rhythm";ClientSize=new Size(520,190);StartPosition=FormStartPosition.CenterScreen;
        FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;MinimizeBox=false;KeyPreview=true;
        BackColor=Color.FromArgb(7,13,19);ForeColor=Color.FromArgb(239,247,249);
        var title=new Label{Text="CROSS RHYTHM",AutoSize=false,Location=new Point(28,25),Size=new Size(464,40),Font=new Font("Segoe UI Semibold",22,FontStyle.Bold),ForeColor=Color.FromArgb(132,232,196)};
        detail=new Label{Text=Japanese()?"ゲーム画面を準備しています":"Preparing the game window",AutoSize=false,Location=new Point(31,72),Size=new Size(458,28),Font=new Font("Segoe UI",10.5f),ForeColor=Color.FromArgb(199,216,223)};
        progress=new ProgressBar{Location=new Point(31,107),Size=new Size(458,12),Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=22};
        close=new Button{Text="Close",Location=new Point(373,137),Size=new Size(116,34),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(20,34,43),ForeColor=Color.White,TabIndex=0};
        close.FlatAppearance.BorderColor=Color.FromArgb(80,112,126);close.Click+=(s,e)=>RequestCancel();
        Controls.Add(title);Controls.Add(detail);Controls.Add(progress);Controls.Add(close);
        Shown+=(s,e)=>StartPlayer();
        KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){RequestCancel();e.Handled=true;e.SuppressKeyPress=true;}};
        FormClosing+=OnClosing;
        timer=new System.Windows.Forms.Timer{Interval=50};timer.Tick+=(s,e)=>Tick();timer.Start();
    }

    static bool Japanese(){return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja",StringComparison.OrdinalIgnoreCase);}

    void StartPlayer(){
        startedAt=DateTime.UtcNow;
        string root=AppDomain.CurrentDomain.BaseDirectory;
        string runtime=Path.Combine(root,"Runtime"),path=Path.Combine(runtime,"CrossRhythmPlayer.exe");
        if(!File.Exists(path)){ShowFailure(Japanese()?"Runtime\\CrossRhythmPlayer.exeが見つかりません":"Runtime\\CrossRhythmPlayer.exe was not found");return;}
        try{
            Icon=Icon.ExtractAssociatedIcon(path);
            var info=new ProcessStartInfo(path,String.Join(" ",arguments.Select(Program.QuoteArgument).ToArray())){UseShellExecute=false,WorkingDirectory=runtime};
            player=Process.Start(info);
            detail.Text=Japanese()?"ゲーム画面を準備しています":"Preparing the game window";
        }catch(Exception ex){ShowFailure((Japanese()?"起動できません: ":"Unable to start: ")+ex.Message);}
    }

    void Tick(){
        if(activate.WaitOne(0)){if(!BringPlayerForward())BringLauncherForward();}
        if(player==null)return;
        try{
            player.Refresh();
            if(player.HasExited){
                if(cancelRequested||playerReady){ExitLauncher();return;}
                ShowFailure((Japanese()?"ゲームを起動できませんでした (終了コード ":"The game could not start (exit code ")+player.ExitCode+")");return;
            }
            if(cancelRequested){
                if(DateTime.UtcNow-cancelAt>TimeSpan.FromSeconds(2))ExitLauncher();
                return;
            }
            if(player.MainWindowHandle!=IntPtr.Zero){
                playerReady=true;progress.MarqueeAnimationSpeed=0;progress.Style=ProgressBarStyle.Continuous;progress.Value=100;
                Hide();ShowInTaskbar=false;
            }else{
                double seconds=(DateTime.UtcNow-startedAt).TotalSeconds;
                detail.Text=(Japanese()?"ゲーム画面を準備しています":"Preparing the game window")+"  "+seconds.ToString("0.0")+" s";
            }
        }catch(InvalidOperationException){ExitLauncher();}
    }

    bool BringPlayerForward(){
        try{
            if(player!=null){player.Refresh();var window=player.MainWindowHandle;if(window!=IntPtr.Zero){Native.ShowWindow(window,Native.SW_RESTORE);Native.SetForegroundWindow(window);return true;}}
        }catch(Exception){}
        return Program.TryActivatePlayer();
    }

    void BringLauncherForward(){
        ShowInTaskbar=true;Show();WindowState=FormWindowState.Normal;TopMost=true;Activate();TopMost=false;
    }

    void RequestCancel(){
        if(cancelRequested)return;
        cancelRequested=true;cancelAt=DateTime.UtcNow;close.Enabled=false;
        detail.Text=Japanese()?"起動を中止しています":"Stopping startup";
        progress.MarqueeAnimationSpeed=0;
        if(player==null){ExitLauncher();return;}
        try{if(!player.HasExited)player.Kill();}catch(Exception){ExitLauncher();}
    }

    void ShowFailure(string message){
        detail.Text=message;progress.MarqueeAnimationSpeed=0;progress.Style=ProgressBarStyle.Continuous;progress.Value=0;
        close.Enabled=true;allowClose=true;
    }

    void ExitLauncher(){
        allowClose=true;timer.Stop();Close();
    }

    void OnClosing(object sender,FormClosingEventArgs e){
        if(allowClose)return;
        e.Cancel=true;RequestCancel();
    }

    protected override void Dispose(bool disposing){
        if(disposing){timer.Dispose();if(player!=null)player.Dispose();}
        base.Dispose(disposing);
    }
}

internal static class Native {
    internal const int SW_RESTORE=9;
    [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr window,int command);
}
}
