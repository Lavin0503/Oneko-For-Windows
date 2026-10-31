using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

// ---------------------------------------------------------------- sprites
static class Sprites
{
    public const int Cell = 32;
    public static Bitmap Original;   // the sheet as shipped
    public static Bitmap Sheet;      // the sheet in the currently chosen cat colour
    static int curColor = 0;
    static Dictionary<string, Bitmap> pixCache = new Dictionary<string, Bitmap>();

    public static readonly string[] ColorNames = { "Classic", "Orange", "Pink", "Sky", "Mint", "Lavender", "Midnight" };
    static readonly Color[] Fills =
    {
        Color.FromArgb(255, 255, 255), Color.FromArgb(255, 166, 77), Color.FromArgb(255, 170, 205),
        Color.FromArgb(150, 205, 255), Color.FromArgb(150, 235, 190), Color.FromArgb(200, 175, 255),
        Color.FromArgb(52, 54, 70)
    };
    static readonly Color[] Outlines =
    {
        Color.FromArgb(0, 0, 0), Color.FromArgb(70, 35, 15), Color.FromArgb(90, 25, 60),
        Color.FromArgb(20, 40, 90), Color.FromArgb(15, 70, 50), Color.FromArgb(45, 25, 95),
        Color.FromArgb(225, 228, 245)
    };
    public static Color SwatchFill(int i) { return Fills[i]; }
    public static Color SwatchOutline(int i) { return Outlines[i]; }

    // sprite name -> flat list of (col,row) pairs on the 8x4 sheet
    static readonly Dictionary<string, int[]> Map = new Dictionary<string, int[]>
    {
        { "idle",         new[] { 3, 3 } },
        { "alert",        new[] { 7, 3 } },
        { "scratchSelf",  new[] { 5, 0, 6, 0, 7, 0 } },
        { "scratchWallN", new[] { 0, 0, 0, 1 } },
        { "scratchWallS", new[] { 7, 1, 6, 2 } },
        { "scratchWallE", new[] { 2, 2, 2, 3 } },
        { "scratchWallW", new[] { 4, 0, 4, 1 } },
        { "tired",        new[] { 3, 2 } },
        { "sleeping",     new[] { 2, 0, 2, 1 } },
        { "N",            new[] { 1, 2, 1, 3 } },
        { "NE",           new[] { 0, 2, 0, 3 } },
        { "E",            new[] { 3, 0, 3, 1 } },
        { "SE",           new[] { 5, 1, 5, 2 } },
        { "S",            new[] { 6, 3, 7, 2 } },
        { "SW",           new[] { 5, 3, 6, 1 } },
        { "W",            new[] { 4, 2, 4, 3 } },
        { "NW",           new[] { 1, 0, 1, 1 } },
    };

    public static void Load()
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("sheet.png"))
        using (Bitmap raw = new Bitmap(s))
            Original = new Bitmap(raw);
        Sheet = Original;
        curColor = 0;
    }

    // recolour the cat (white fill / black outline -> chosen pair)
    public static void ApplyColor(int idx)
    {
        if (Original == null) return;
        if (idx < 0 || idx >= Fills.Length) idx = 0;
        if (idx == curColor) return;
        curColor = idx;
        pixCache.Clear();
        Bitmap old = Sheet;
        if (idx == 0) Sheet = Original;
        else
        {
            Bitmap b = new Bitmap(Original.Width, Original.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < Original.Height; y++)
                for (int x = 0; x < Original.Width; x++)
                {
                    Color c = Original.GetPixel(x, y);
                    if (c.A == 0) { b.SetPixel(x, y, c); continue; }
                    bool light = (c.R + c.G + c.B) > 380;
                    b.SetPixel(x, y, light ? Fills[idx] : Outlines[idx]);
                }
            Sheet = b;
        }
        if (old != null && old != Original && old != Sheet) old.Dispose();
    }

    public static Rectangle Src(string name, int frame)
    {
        int[] f = Map[name];
        int n = f.Length / 2;
        int i = ((frame % n) + n) % n;
        return new Rectangle(f[i * 2] * Cell, f[i * 2 + 1] * Cell, Cell, Cell);
    }

    public static void Draw(Graphics g, Rectangle dest, string name, int frame)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(Sheet, dest, Src(name, frame), GraphicsUnit.Pixel);
    }

    public static void DrawF(Graphics g, RectangleF dest, Rectangle src)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(Sheet, dest, new RectangleF(src.X, src.Y, src.Width, src.Height), GraphicsUnit.Pixel);
    }

    // chunky "terminal" version of one sprite cell: block x block squares, optional green phosphor + scanlines
    public static Bitmap Pixelated(Rectangle src, int block, bool tint)
    {
        string key = src.X + "," + src.Y + "," + block + "," + tint;
        Bitmap bmp;
        if (pixCache.TryGetValue(key, out bmp)) return bmp;
        bmp = new Bitmap(Cell, Cell, PixelFormat.Format32bppArgb);
        for (int by = 0; by < Cell; by += block)
            for (int bx = 0; bx < Cell; bx += block)
            {
                int n = 0, tot = 0, sr = 0, sg = 0, sb = 0;
                int ye = Math.Min(by + block, Cell), xe = Math.Min(bx + block, Cell);
                for (int y = by; y < ye; y++)
                    for (int x = bx; x < xe; x++)
                    {
                        tot++;
                        Color c = Sheet.GetPixel(src.X + x, src.Y + y);
                        if (c.A > 128) { n++; sr += c.R; sg += c.G; sb += c.B; }
                    }
                if (n * 2 < tot) continue;
                Color o = Color.FromArgb(sr / n, sg / n, sb / n);
                if (tint)
                {
                    float lum = (0.3f * o.R + 0.59f * o.G + 0.11f * o.B) / 255f;
                    o = Color.FromArgb((int)(10 + 30 * lum), (int)(70 + 185 * lum), (int)(40 + 90 * lum));
                }
                for (int y = by; y < ye; y++)
                {
                    Color row = (tint && (y & 1) == 1)
                        ? Color.FromArgb((int)(o.R * 0.72f), (int)(o.G * 0.72f), (int)(o.B * 0.72f)) : o;
                    for (int x = bx; x < xe; x++) bmp.SetPixel(x, y, row);
                }
            }
        pixCache[key] = bmp;
        return bmp;
    }

    public static void DrawPix(Graphics g, RectangleF dest, Rectangle src, int block, bool tint)
    {
        if (block <= 1 && !tint) { DrawF(g, dest, src); return; }
        Bitmap b = Pixelated(src, Math.Max(1, block), tint);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(b, dest, new RectangleF(0, 0, Cell, Cell), GraphicsUnit.Pixel);
    }

    public static Icon MakeIcon()
    {
        using (Bitmap b = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(b))
                Draw(g, new Rectangle(0, 0, 32, 32), "idle", 0);
            return Icon.FromHandle(b.GetHicon());
        }
    }
}

// ---------------------------------------------------------------- settings
static class Cfg
{
    public static int Speed = 10, Size = 2, Anim = 5, Follow = 48, Sleepy = 2, Opacity = 100, ColorIdx = 0, Pixel = 2;
    public static bool OnTop = true, Sprint = false, WallScratch = true, Terminal = true, TermTint = false, OpenOnLaunch = true, Flee = false, HideFullscreen = true, StartAnim = true;
    public static bool Paused = false;   // not saved
    public static event EventHandler Changed;

    public static void Notify()
    {
        Sprites.ApplyColor(ColorIdx);
        if (Changed != null) Changed(null, EventArgs.Empty);
    }

    static string Dir
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Oneko"); }
    }
    static string FilePath { get { return Path.Combine(Dir, "settings3.ini"); } }

    static int Clamp(int v, int lo, int hi) { return Math.Max(lo, Math.Min(hi, v)); }

    public static void Defaults()
    {
        Speed = 10; Size = 2; Anim = 5; Follow = 48; Sleepy = 2; Opacity = 100; ColorIdx = 0; Pixel = 2;
        OnTop = true; Sprint = false; WallScratch = true; Terminal = true; TermTint = false; OpenOnLaunch = true; Flee = false; HideFullscreen = true; StartAnim = true;
    }

    public static void Load()
    {
        Defaults();
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int i = line.IndexOf('=');
                if (i < 1) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                int n;
                bool isInt = int.TryParse(v, out n);
                bool b = (v == "1");
                switch (k)
                {
                    case "Speed":   if (isInt) Speed = Clamp(n, 1, 40); break;
                    case "Size":    if (isInt) Size = Clamp(n, 1, 8); break;
                    case "Anim":    if (isInt) Anim = Clamp(n, 1, 10); break;
                    case "Follow":  if (isInt) Follow = Clamp(n, 16, 200); break;
                    case "Sleepy":  if (isInt) Sleepy = Clamp(n, 0, 10); break;
                    case "Opacity": if (isInt) Opacity = Clamp(n, 20, 100); break;
                    case "Color":   if (isInt) ColorIdx = Clamp(n, 0, 6); break;
                    case "Pixel":   if (isInt) Pixel = Clamp(n, 2, 6); break;
                    case "OnTop": OnTop = b; break;
                    case "Sprint": Sprint = b; break;
                    case "Wall": WallScratch = b; break;
                    case "Terminal": Terminal = b; break;
                    case "Tint": TermTint = b; break;
                    case "OpenOnLaunch": OpenOnLaunch = b; break;
                    case "Flee": Flee = b; break;
                    case "FullscreenHide": HideFullscreen = b; break;
                    case "StartAnim": StartAnim = b; break;
                }
            }
        }
        catch { }
    }

    static string B(bool v) { return v ? "1" : "0"; }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath,
                "Speed=" + Speed + "\r\nSize=" + Size + "\r\nAnim=" + Anim + "\r\nFollow=" + Follow +
                "\r\nSleepy=" + Sleepy + "\r\nOpacity=" + Opacity + "\r\nColor=" + ColorIdx + "\r\nPixel=" + Pixel +
                "\r\nOnTop=" + B(OnTop) + "\r\nSprint=" + B(Sprint) + "\r\nWall=" + B(WallScratch) +
                "\r\nTerminal=" + B(Terminal) + "\r\nTint=" + B(TermTint) + "\r\nOpenOnLaunch=" + B(OpenOnLaunch) + "\r\nFlee=" + B(Flee) + "\r\nFullscreenHide=" + B(HideFullscreen) + "\r\nStartAnim=" + B(StartAnim) + "\r\n");
        }
        catch { }
    }
}

static class Links
{
    public static void OpenCreations()
    {
        try
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("https://lav1n.is-a.dev");
            psi.UseShellExecute = true;
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }
}

// ---------------------------------------------------------------- "what window is under the cat?"
static class TermDetect
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);

    static bool broken = false;
    static uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
    static Dictionary<uint, string> names = new Dictionary<uint, string>();
    static DateTime cacheStamp = DateTime.Now;

    static readonly HashSet<string> TermClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "mintty", "VirtualConsoleClass",
        "Alacritty", "org.wezfurlong.wezterm", "PuTTY", "Hyper"
    };
    static readonly HashSet<string> TermProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "powershell", "powershell_ise", "pwsh", "windowsterminal", "wt", "conhost", "openconsole",
        "mintty", "bash", "wsl", "wslhost", "alacritty", "wezterm-gui", "wezterm", "kitty", "hyper",
        "tabby", "conemu", "conemu64", "cmder", "putty", "ubuntu", "debian", "kali-linux"
    };

    static string ProcName(uint pid)
    {
        if ((DateTime.Now - cacheStamp).TotalSeconds > 60) { names.Clear(); cacheStamp = DateTime.Now; }
        string n;
        if (names.TryGetValue(pid, out n)) return n;
        try { n = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { n = ""; }
        names[pid] = n;
        return n;
    }

    // true while a borderless full-screen app (game, video) is in front
    public static bool FullscreenForeground()
    {
        if (broken) return false;
        try
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == myPid) return false;
            StringBuilder sb = new StringBuilder(64);
            GetClassName(h, sb, 64);
            string c = sb.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
            if ((GetWindowLong(h, -16) & 0xC00000) == 0xC00000) return false;   // has a title bar: just maximised
            RECT r;
            if (!GetWindowRect(h, out r)) return false;
            Rectangle b = Screen.FromHandle(h).Bounds;
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
        catch { return false; }
    }

    // the first visible, non-click-through window (top of the z-order down) containing the point decides
    public static bool IsTerminalAt(int x, int y)
    {
        if (broken) return false;
        try
        {
            IntPtr h = GetTopWindow(IntPtr.Zero);
            int guard = 0;
            while (h != IntPtr.Zero && guard++ < 500)
            {
                if (IsWindowVisible(h) && !IsIconic(h))
                {
                    RECT r;
                    if (GetWindowRect(h, out r) && x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom)
                    {
                        uint pid;
                        GetWindowThreadProcessId(h, out pid);
                        if (pid != myPid)
                        {
                            int ex = GetWindowLong(h, -20);
                            bool clickThrough = (ex & 0x20) != 0 && (ex & 0x80000) != 0;
                            int cloaked = 0;
                            try { DwmGetWindowAttribute(h, 14, out cloaked, 4); } catch { }
                            if (!clickThrough && cloaked == 0)
                            {
                                StringBuilder sb = new StringBuilder(256);
                                GetClassName(h, sb, 256);
                                if (TermClasses.Contains(sb.ToString())) return true;
                                return TermProcs.Contains(ProcName(pid));
                            }
                        }
                    }
                }
                h = GetWindow(h, 2);   // GW_HWNDNEXT
            }
        }
        catch { broken = true; }
        return false;
    }
}

// ---------------------------------------------------------------- the cat
class CatForm : Form
{
    static readonly Color Key = Color.FromArgb(255, 0, 255);

    Timer timer = new Timer();
    Timer fx = new Timer();
    Random rnd = new Random();

    int mult, cellPx;
    double px, py;                       // cat centre, screen coords
    int frameCount, idleTime, idleAnimFrame;
    string idleAnim = null;
    Rectangle src = Sprites.Src("idle", 0);

    bool overTerm, wasPaused, fsHidden, lastStartAnim;
    float termT, spawnP = 1f;
    int curBlock = 1;
    bool curTint;

    public CatForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Key;
        TransparencyKey = Key;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        Point m = Cursor.Position;
        px = m.X - 150; py = m.Y - 150;

        timer.Tick += OnTick;
        fx.Interval = 30;
        fx.Tick += OnFx;
        Cfg.Changed += delegate { ApplyCfg(); };
        wasPaused = Cfg.Paused;
        lastStartAnim = Cfg.StartAnim;
        StartSpawn();
        ApplyCfg();
    }

    static float Ease(float t) { return 1f - (float)Math.Pow(1f - Math.Max(0f, Math.Min(1f, t)), 3); }

    // startup animation: the cat resolves out of big pixels while fading in
    void StartSpawn() { spawnP = Cfg.StartAnim ? 0f : 1f; }

    void UpdateOpacity()
    {
        double f = spawnP < 1f ? Math.Min(1f, spawnP * 2.5f) : 1f;
        Opacity = Cfg.Opacity / 100.0 * f;
    }

    void ApplyCfg()
    {
        if (wasPaused && !Cfg.Paused) StartSpawn();
        if (!lastStartAnim && Cfg.StartAnim) StartSpawn();   // toggled on: replay it as a preview
        wasPaused = Cfg.Paused;
        lastStartAnim = Cfg.StartAnim;

        mult = Cfg.Size;
        cellPx = Sprites.Cell * mult;
        ClientSize = new Size(cellPx, cellPx);
        TopMost = Cfg.OnTop;
        UpdateOpacity();
        timer.Interval = 150 - 10 * Cfg.Anim;
        timer.Enabled = !Cfg.Paused;
        fx.Enabled = !Cfg.Paused;
        MoveWindow();
        bool hide = Cfg.Paused || fsHidden;
        if (hide && Visible) Hide();
        else if (!hide && !Visible) Show();
        Invalidate();
    }

    // click-through, no focus, hidden from Alt+Tab
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000     // WS_EX_LAYERED
                       |  0x20        // WS_EX_TRANSPARENT (clicks pass through)
                       |  0x80        // WS_EX_TOOLWINDOW
                       |  0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Key);
        Sprites.DrawPix(g, new RectangleF(0, 0, cellPx, cellPx), src, curBlock, curTint);
    }

    // smooth pixelate transitions (terminal + startup)
    void OnFx(object sender, EventArgs ev)
    {
        bool want = Cfg.Terminal && overTerm && !Cfg.Paused;
        termT += (want ? 1f : -1f) * 0.03f / 0.22f;
        if (termT < 0f) termT = 0f;
        if (termT > 1f) termT = 1f;
        int blk = 1 + (int)Math.Round(termT * (Cfg.Pixel - 1));
        if (spawnP < 1f)
        {
            spawnP = Math.Min(1f, spawnP + 0.03f / 0.9f);
            blk = Math.Max(blk, 1 + (int)Math.Round((1f - Ease(spawnP)) * 7f));
            UpdateOpacity();
        }
        bool tint = Cfg.TermTint && termT > 0.5f;
        if (blk != curBlock || tint != curTint) { curBlock = blk; curTint = tint; Invalidate(); }
    }

    void MoveWindow()
    {
        Rectangle b = SystemInformation.VirtualScreen;
        px = Math.Max(b.Left + cellPx / 2, Math.Min(b.Right - cellPx / 2, px));
        py = Math.Max(b.Top + cellPx / 2, Math.Min(b.Bottom - cellPx / 2, py));
        Location = new Point((int)(px - cellPx / 2), (int)(py - cellPx / 2));
    }

    void SetSprite(string name, int frame)
    {
        Rectangle r = Sprites.Src(name, frame);
        if (r != src) { src = r; Invalidate(); }
    }

    void ResetIdle() { idleAnim = null; idleAnimFrame = 0; }

    // ---- real screen edges (works across several monitors) ----
    static bool IsOutside(int x, int y)
    {
        foreach (Screen sc in Screen.AllScreens)
            if (sc.Bounds.Contains(x, y)) return false;
        return true;
    }

    bool NearWall(Point m)
    {
        int pad = cellPx * 3 / 2;
        return IsOutside(m.X - pad, m.Y) || IsOutside(m.X + pad, m.Y) || IsOutside(m.X, m.Y - pad) || IsOutside(m.X, m.Y + pad);
    }

    // which wall (W/E/N/S) is the cat up against, with the cursor on that side?
    string WallAtCat()
    {
        Point m = Cursor.Position;
        int pad = cellPx * 3 / 2;
        if (IsOutside((int)px - pad, (int)py) && m.X < px + 8) return "W";
        if (IsOutside((int)px + pad, (int)py) && m.X > px - 8) return "E";
        if (IsOutside((int)px, (int)py - pad) && m.Y < py + 8) return "N";
        if (IsOutside((int)px, (int)py + pad) && m.Y > py - 8) return "S";
        return null;
    }

    void Idle()
    {
        idleTime++;

        if (idleAnim == null)
        {
            string wall = (Cfg.WallScratch && !Cfg.Flee) ? WallAtCat() : null;
            if (wall != null && idleTime > 2) idleAnim = "scratchWall" + wall;
            else if (Cfg.Sleepy > 0 && idleTime > 10 && rnd.Next(400 / Cfg.Sleepy) == 0)
                idleAnim = rnd.Next(2) == 0 ? "sleeping" : "scratchSelf";
        }

        switch (idleAnim)
        {
            case "sleeping":
                if (idleAnimFrame < 8) { SetSprite("tired", 0); break; }
                SetSprite("sleeping", idleAnimFrame / 4);
                if (idleAnimFrame > 192) ResetIdle();
                break;
            case "scratchWallN":
            case "scratchWallS":
            case "scratchWallE":
            case "scratchWallW":
            case "scratchSelf":
                SetSprite(idleAnim, idleAnimFrame);
                if (idleAnimFrame > 9) ResetIdle();
                break;
            default:
                SetSprite("idle", 0);
                return;
        }
        idleAnimFrame++;
    }

    void OnTick(object sender, EventArgs ev)
    {
        frameCount++;

        bool fs = Cfg.HideFullscreen && TermDetect.FullscreenForeground();
        if (fs != fsHidden) { fsHidden = fs; ApplyCfg(); }
        if (fsHidden) return;

        overTerm = Cfg.Terminal && TermDetect.IsTerminalAt((int)px, (int)py);
#if TERMTEST
        overTerm = Cfg.Terminal;
#endif

        int speed = Cfg.Speed * mult;
        Point m = Cursor.Position;
        double dx = px - m.X, dy = py - m.Y;
        double dist = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
        double stop = Cfg.Follow * mult;

        if (Cfg.Flee)
        {
            if (dist > stop * 3.5) { Idle(); return; }
            dx = -dx; dy = -dy;                 // run away instead of toward
        }
        else
        {
            if (Cfg.WallScratch && NearWall(m)) stop = cellPx * 0.6;   // cursor parked at an edge: go claw the wall
            if (dist < speed || dist < stop) { Idle(); return; }
        }

        ResetIdle();

        if (idleTime > 1)   // was resting: show the "!" pose before running
        {
            SetSprite("alert", 0);
            idleTime = Math.Min(idleTime, 7);
            idleTime--;
            return;
        }

        if (Cfg.Sprint && !Cfg.Flee && dist > 220 * mult) speed *= 2;

        string dir = "";
        if (dy / dist > 0.5) dir += "N";
        if (dy / dist < -0.5) dir += "S";
        if (dx / dist > 0.5) dir += "W";
        if (dx / dist < -0.5) dir += "E";
        SetSprite(dir, frameCount);

        px -= dx / dist * speed;
        py -= dy / dist * speed;
        MoveWindow();
    }
}

// ---------------------------------------------------------------- settings window (custom drawn)
class SettingsForm : Form
{
    public event EventHandler HiddenToTray;

    // logical layout size - everything is scaled by 's' for the real screen
    const float W = 760f, H = 540f, HeaderH = 132f;

    static readonly Color Bg      = Color.FromArgb(18, 19, 29);
    static readonly Color CardC   = Color.FromArgb(27, 29, 44);
    static readonly Color CardB   = Color.FromArgb(44, 47, 70);
    static readonly Color TextC   = Color.FromArgb(236, 238, 250);
    static readonly Color Muted   = Color.FromArgb(139, 145, 176);
    static readonly Color Accent  = Color.FromArgb(139, 124, 255);
    static readonly Color Pink    = Color.FromArgb(255, 122, 184);
    static readonly Color Cyan    = Color.FromArgb(70, 200, 255);
    static readonly Color TrackC  = Color.FromArgb(46, 49, 74);
    static readonly Color Danger  = Color.FromArgb(255, 107, 129);

    static readonly string[] TabNames = { "Cat", "Behavior", "Terminal", "General" };
    static readonly string[] TabTitles = { "APPEARANCE", "BEHAVIOR", "TERMINAL VERSION", "GENERAL" };

    class UiSlider
    {
        public int Tab;
        public string Title, Desc;
        public int Min, Max;
        public Func<int> Get;
        public Action<int> Set;
        public Func<int, string> Fmt;
        public RectangleF Rect;
        public float Disp, Hov, Drag;
        public bool Hover, Dragging;
    }
    class UiToggle
    {
        public int Tab;
        public string Title, Desc;
        public Func<bool> Get;
        public Action Flip;
        public RectangleF Rect;
        public float On, Hov;
        public bool Hover;
    }
    class UiSwatch
    {
        public int Index;
        public RectangleF Rect;
        public float Hov, Sel;
        public bool Hover;
    }
    class UiText
    {
        public int Tab;
        public string Text;
        public RectangleF Rect;
        public bool Accent;
    }
    class UiTab
    {
        public string Text;
        public RectangleF Rect;
        public float Hov;
        public bool Hover;
    }
    class UiButton
    {
        public int Group;              // 1 promo card, 2 actions, 9 window buttons
        public string Text;
        public Func<string> TextFn;
        public int Style;              // 0 primary, 1 secondary, 2 danger, 3 minimise, 4 close, 5 link
        public RectangleF Rect;
        public float Hov, Press;
        public bool Hover, Down;
        public Action Click;
    }

    float s = 1f;
    float intro = 0f, clock = 0f;
    bool autostart;
    UiSlider dragSlider;
    List<UiSlider> sliders = new List<UiSlider>();
    List<UiToggle> toggles = new List<UiToggle>();
    List<UiSwatch> swatches = new List<UiSwatch>();
    List<UiText> texts = new List<UiText>();
    List<UiTab> tabs = new List<UiTab>();
    List<UiButton> buttons = new List<UiButton>();

    RectangleF tabBar = new RectangleF(24, 144, 448, 36);
    RectangleF leftCard = new RectangleF(24, 192, 448, 328);
    RectangleF promoCard = new RectangleF(488, 384, 248, 136);
    RectangleF stage = new RectangleF(440, 40, 296, 82);

    int curTab = 0, prevTab = 0;
    float tabT = 1f, indX, indW;

    Font fTitle, fSub, fLabel, fDesc, fBtn, fSection, fValue, fMono;
    StringFormat sfTight, sfWrap;
    float ga = 1f;                          // global alpha used by A()

    Timer anim = new Timer();
    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    long lastMs;

    // header stage cat
    float catX = 470f, animT, idleT = 5f, alertT, dustT, stageTerm;
    string pose = "idle";
    int poseFrame;
    List<float[]> dust = new List<float[]>();
    float[] starX = new float[14], starY = new float[14], starP = new float[14];

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public SettingsForm(Icon icon)
    {
        Text = "Oneko for Windows";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        KeyPreview = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Opacity = 0;

        try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) s = ScaleFor((int)g.DpiX); }
        catch { s = 1f; }
        ClientSize = new Size((int)Math.Round(W * s), (int)Math.Round(H * s));

        fTitle = new Font("Segoe UI", 30f, FontStyle.Bold, GraphicsUnit.Pixel);
        fSub = new Font("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
        fLabel = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
        fDesc = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
        fBtn = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
        fSection = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
        fValue = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
        fMono = new Font("Consolas", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
        sfTight = new StringFormat(StringFormat.GenericTypographic);
        sfTight.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
        sfTight.Trimming = StringTrimming.None;
        sfWrap = new StringFormat();
        sfWrap.Trimming = StringTrimming.None;

        Random r = new Random(7);
        for (int i = 0; i < starX.Length; i++)
        {
            starX[i] = stage.X + 12 + (float)r.NextDouble() * (stage.Width - 24);
            starY[i] = stage.Y + 8 + (float)r.NextDouble() * (stage.Height - 34);
            starP[i] = (float)r.NextDouble() * 6.28f;
        }

        autostart = GetAutostart();
        BuildUi();
        indX = tabs[0].Rect.X; indW = tabs[0].Rect.Width;

        anim.Interval = 15;
        anim.Tick += OnAnim;
    }

    // ---------------------------------------------------------- build
    void BuildUi()
    {
        // tabs
        float tw = tabBar.Width / TabNames.Length;
        for (int i = 0; i < TabNames.Length; i++)
        {
            UiTab t = new UiTab();
            t.Text = TabNames[i];
            t.Rect = new RectangleF(tabBar.X + i * tw, tabBar.Y, tw, tabBar.Height);
            tabs.Add(t);
        }

        float x = leftCard.X + 24, w = leftCard.Width - 48, y0 = leftCard.Y + 50;

        // --- Cat tab
        AddSlider(0, "Cat size", "pixel scale", 1, 8, x, y0, w,
            delegate { return Cfg.Size; }, delegate (int v) { Cfg.Size = v; }, delegate (int v) { return v + "x"; });
        AddSlider(0, "Opacity", "see-through cat", 20, 100, x, y0 + 62, w,
            delegate { return Cfg.Opacity; }, delegate (int v) { Cfg.Opacity = v; }, delegate (int v) { return v + "%"; });
        AddSlider(0, "Animation speed", "leg and tail speed", 1, 10, x, y0 + 124, w,
            delegate { return Cfg.Anim; }, delegate (int v) { Cfg.Anim = v; }, delegate (int v) { return v.ToString(); });
        for (int i = 0; i < Sprites.ColorNames.Length; i++)
        {
            UiSwatch sw = new UiSwatch();
            sw.Index = i;
            sw.Rect = new RectangleF(x + 2 + i * 42, y0 + 186 + 26, 30, 30);
            sw.Sel = Cfg.ColorIdx == i ? 1f : 0f;
            swatches.Add(sw);
        }

        // --- Behavior tab
        AddSlider(1, "Run speed", "how fast she runs", 1, 40, x, y0, w,
            delegate { return Cfg.Speed; }, delegate (int v) { Cfg.Speed = v; }, delegate (int v) { return v.ToString(); });
        AddSlider(1, "Follow distance", "how close she gets", 16, 200, x, y0 + 54, w,
            delegate { return Cfg.Follow; }, delegate (int v) { Cfg.Follow = v; }, delegate (int v) { return v + " px"; });
        AddSlider(1, "Sleepiness", "naps and scratching", 0, 10, x, y0 + 108, w,
            delegate { return Cfg.Sleepy; }, delegate (int v) { Cfg.Sleepy = v; }, delegate (int v) { return v == 0 ? "Off" : v.ToString(); });
        AddToggle(1, "Sprint when far away", "runs faster to catch up", x, y0 + 156, w,
            delegate { return Cfg.Sprint; }, delegate { Cfg.Sprint = !Cfg.Sprint; Cfg.Save(); Cfg.Notify(); });
        AddToggle(1, "Scratch screen edges", "claws the wall when you park the cursor there", x, y0 + 194, w,
            delegate { return Cfg.WallScratch; }, delegate { Cfg.WallScratch = !Cfg.WallScratch; Cfg.Save(); Cfg.Notify(); });
        AddToggle(1, "Scaredy cat mode", "runs away from your cursor", x, y0 + 232, w,
            delegate { return Cfg.Flee; }, delegate { Cfg.Flee = !Cfg.Flee; Cfg.Save(); Cfg.Notify(); });

        // --- Terminal tab
        AddText(2, "When the cat walks over a terminal (cmd, PowerShell, Windows Terminal, Git Bash, WSL...) she turns into chunky pixels.",
            x, y0 - 4, w, 44, false);
        AddToggle(2, "Terminal Version", "pixelate the cat over terminals", x, y0 + 62, w,
            delegate { return Cfg.Terminal; }, delegate { Cfg.Terminal = !Cfg.Terminal; Cfg.Save(); Cfg.Notify(); });
        AddSlider(2, "Pixelation", "bigger = chunkier", 2, 6, x, y0 + 118, w,
            delegate { return Cfg.Pixel; }, delegate (int v) { Cfg.Pixel = v; }, delegate (int v) { return v + "x"; });
        AddToggle(2, "Green terminal tint", "phosphor green with scanlines", x, y0 + 184, w,
            delegate { return Cfg.TermTint; }, delegate { Cfg.TermTint = !Cfg.TermTint; Cfg.Save(); Cfg.Notify(); });
        AddText(2, "Tip: this tab previews the effect on the cat up top.", x, y0 + 236, w, 20, true);

        // --- General tab
        AddToggle(3, "Keep on top", "stay above other windows", x, y0, w,
            delegate { return Cfg.OnTop; }, delegate { Cfg.OnTop = !Cfg.OnTop; Cfg.Save(); Cfg.Notify(); });
        AddToggle(3, "Start with Windows", "launch quietly at login", x, y0 + 44, w,
            delegate { return autostart; }, delegate { autostart = !autostart; SetAutostart(autostart); });
        AddToggle(3, "Open settings on launch", "turn off to start in the tray", x, y0 + 88, w,
            delegate { return Cfg.OpenOnLaunch; }, delegate { Cfg.OpenOnLaunch = !Cfg.OpenOnLaunch; Cfg.Save(); });
        AddToggle(3, "Startup animation", "the cat pixel-pops in when she starts", x, y0 + 132, w,
            delegate { return Cfg.StartAnim; }, delegate { Cfg.StartAnim = !Cfg.StartAnim; Cfg.Save(); Cfg.Notify(); });
        AddToggle(3, "Hide in fullscreen apps", "hides during games and videos", x, y0 + 176, w,
            delegate { return Cfg.HideFullscreen; }, delegate { Cfg.HideFullscreen = !Cfg.HideFullscreen; Cfg.Save(); Cfg.Notify(); });
        AddText(3, "She is click-through, so she never blocks your clicks.", x, y0 + 228, w, 18, false);
        AddText(3, "Oneko for Windows  v1.1.0   |   made by Lav1nRules", x, y0 + 252, w, 20, true);

        // --- right column
        float bx = promoCard.X, bw = promoCard.Width;
        AddButton(1, "See more creations", null, 5, bx + 16, promoCard.Y + 72, bw - 32,
            delegate { Links.OpenCreations(); });
        float by = 148;
        AddButton(2, null, delegate { return Cfg.Paused ? "Resume cat" : "Pause cat"; }, 0, bx, by, bw,
            delegate { Cfg.Paused = !Cfg.Paused; Cfg.Notify(); });
        AddButton(2, "Reset to defaults", null, 1, bx, by + 44, bw,
            delegate { Cfg.Defaults(); Cfg.Save(); Cfg.Notify(); });
        AddButton(2, "Hide to tray", null, 1, bx, by + 88, bw, delegate { Close(); });
        AddButton(2, "Quit Oneko", null, 2, bx, by + 132, bw, delegate { Application.Exit(); });

        UiButton min = new UiButton(); min.Group = 9; min.Style = 3; min.Rect = new RectangleF(W - 92, 0, 46, 34);
        min.Click = delegate { WindowState = FormWindowState.Minimized; };
        UiButton cls = new UiButton(); cls.Group = 9; cls.Style = 4; cls.Rect = new RectangleF(W - 46, 0, 46, 34);
        cls.Click = delegate { Close(); };
        buttons.Add(min); buttons.Add(cls);
    }

    void AddSlider(int tab, string title, string desc, int min, int max, float x, float y, float w,
                   Func<int> get, Action<int> set, Func<int, string> fmt)
    {
        UiSlider sl = new UiSlider();
        sl.Tab = tab; sl.Title = title; sl.Desc = desc; sl.Min = min; sl.Max = max;
        sl.Get = get; sl.Set = set; sl.Fmt = fmt;
        sl.Rect = new RectangleF(x, y, w, 46);
        sl.Disp = get();
        sliders.Add(sl);
    }

    void AddToggle(int tab, string title, string desc, float x, float y, float w, Func<bool> get, Action flip)
    {
        UiToggle t = new UiToggle();
        t.Tab = tab; t.Title = title; t.Desc = desc; t.Get = get; t.Flip = flip;
        t.Rect = new RectangleF(x, y, w, 38);
        t.On = get() ? 1f : 0f;
        toggles.Add(t);
    }

    void AddText(int tab, string text, float x, float y, float w, float h, bool accent)
    {
        UiText t = new UiText();
        t.Tab = tab; t.Text = text; t.Rect = new RectangleF(x, y, w, h); t.Accent = accent;
        texts.Add(t);
    }

    void AddButton(int group, string text, Func<string> fn, int style, float x, float y, float w, Action click)
    {
        UiButton b = new UiButton();
        b.Group = group; b.Text = text; b.TextFn = fn; b.Style = style; b.Click = click;
        b.Rect = new RectangleF(x, y, w, 38);
        buttons.Add(b);
    }

    static float ScaleFor(int dpi)
    {
        float v = dpi / 96f;
        try { if (Screen.PrimaryScreen.Bounds.Height >= 1400 && v < 1.25f) v = 1.25f; } catch { }
        return v;
    }

    // dragging the window to a monitor with different scaling: resize to match
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0)   // WM_DPICHANGED
        {
            s = ScaleFor((int)((long)m.WParam & 0xFFFF));
            ClientSize = new Size((int)Math.Round(W * s), (int)Math.Round(H * s));
            Invalidate();
            return;
        }
        base.WndProc(ref m);
    }

    // ---------------------------------------------------------- window plumbing
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW
            cp.Style |= 0x20000;        // WS_MINIMIZEBOX (taskbar click-to-minimise)
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int pref = 2; DwmSetWindowAttribute(Handle, 33, ref pref, 4); } catch { } // rounded corners on Win11
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) { watch.Restart(); lastMs = 0; anim.Start(); }
        else anim.Stop();
    }

    public void ShowSettings()
    {
        if (!Visible || WindowState == FormWindowState.Minimized) { intro = 0f; Opacity = 0; catX = stage.Left - 80f; idleT = 0f; alertT = 0f; }
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        TopMost = true; TopMost = false;   // nudge to the front
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (HiddenToTray != null) HiddenToTray(this, EventArgs.Empty);
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    static bool GetAutostart()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("Oneko") != null;
        }
        catch { return false; }
    }

    static void SetAutostart(bool on)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue("Oneko", "\"" + Application.ExecutablePath + "\" --tray");
                else k.DeleteValue("Oneko", false);
            }
        }
        catch { }
    }

    // ---------------------------------------------------------- animation
    static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
    static float Ease(float t) { t = Clamp01(t); return 1f - (float)Math.Pow(1f - t, 3); }
    static Color Lerp(Color a, Color b, float t)
    {
        t = Clamp01(t);
        return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                              (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
    // moves cur toward target; returns true while still moving
    static bool Step(ref float cur, float target, float rate, float dt)
    {
        float d = target - cur;
        if (Math.Abs(d) < 0.002f) { cur = target; return false; }
        cur += d * (1f - (float)Math.Exp(-rate * dt));
        return true;
    }
    Color A(Color c) { return Color.FromArgb((int)(c.A * ga), c.R, c.G, c.B); }
    float App(int k) { return Ease((intro - 0.10f * k - 0.05f) / 0.45f); }

    void SwitchTab(int i)
    {
        if (i == curTab) return;
        prevTab = curTab; curTab = i; tabT = 0f;
        foreach (UiSlider sl in sliders) { sl.Hover = false; sl.Dragging = false; }
        foreach (UiToggle t in toggles) t.Hover = false;
        foreach (UiSwatch w in swatches) w.Hover = false;
    }

    void OnAnim(object sender, EventArgs ev)
    {
        long now = watch.ElapsedMilliseconds;
        float dt = Math.Min(0.05f, (now - lastMs) / 1000f);
        lastMs = now;
        clock += dt;
        bool dirty = false;

        if (intro < 1.3f)
        {
            intro += dt;
            Opacity = Ease(intro / 0.3f);
            if (intro >= 0.3f && Opacity < 1.0) Opacity = 1.0;
            dirty = true;
        }

        if (tabT < 1f) { tabT = Math.Min(1f, tabT + dt / 0.28f); dirty = true; }
        if (Step(ref indX, tabs[curTab].Rect.X, 14f, dt)) dirty = true;
        if (Step(ref indW, tabs[curTab].Rect.Width, 14f, dt)) dirty = true;
        foreach (UiTab t in tabs) if (Step(ref t.Hov, t.Hover ? 1f : 0f, 14f, dt)) dirty = true;

        foreach (UiSlider sl in sliders)
        {
            if (Step(ref sl.Disp, sl.Get(), 16f, dt)) dirty = true;
            if (Step(ref sl.Hov, sl.Hover || sl.Dragging ? 1f : 0f, 14f, dt)) dirty = true;
            if (Step(ref sl.Drag, sl.Dragging ? 1f : 0f, 18f, dt)) dirty = true;
        }
        foreach (UiToggle t in toggles)
        {
            if (Step(ref t.On, t.Get() ? 1f : 0f, 16f, dt)) dirty = true;
            if (Step(ref t.Hov, t.Hover ? 1f : 0f, 14f, dt)) dirty = true;
        }
        foreach (UiSwatch w in swatches)
        {
            if (Step(ref w.Sel, Cfg.ColorIdx == w.Index ? 1f : 0f, 16f, dt)) dirty = true;
            if (Step(ref w.Hov, w.Hover ? 1f : 0f, 16f, dt)) dirty = true;
        }
        foreach (UiButton b in buttons)
        {
            if (Step(ref b.Hov, b.Hover ? 1f : 0f, 16f, dt)) dirty = true;
            if (Step(ref b.Press, b.Down && b.Hover ? 1f : 0f, 30f, dt)) dirty = true;
        }

        // the header cat turns pixelated while the Terminal tab is open
        if (Step(ref stageTerm, (curTab == 2 && Cfg.Terminal) ? 1f : 0f, 9f, dt)) dirty = true;

        UpdateStage(dt);

        if (dirty) Invalidate();
        else Invalidate(new Rectangle(0, 0, ClientSize.Width, (int)(HeaderH * s) + 2));
    }

    void UpdateStage(float dt)
    {
        Point cp = PointToClient(Cursor.Position);
        bool hasMouse = ClientRectangle.Contains(cp) && !Cfg.Paused;
        float mx = cp.X / s;
        float minX = stage.Left + 8, maxX = stage.Right - 8 - 64;
        float target = catX;
        bool chase = false;
        if (hasMouse)
        {
            target = Math.Max(minX, Math.Min(maxX, mx - 32));
            chase = Math.Abs(target - catX) > 26f;
        }
        if (intro < 1.5f) { target = stage.X + stage.Width / 2f - 32f; chase = Math.Abs(target - catX) > 6f; }   // run-in on startup
        if (Cfg.Paused) idleT = Math.Max(idleT, 4f);

        if (chase && idleT > 0.8f && alertT <= 0f) { alertT = 0.35f; idleT = 0f; }

        if (alertT > 0f)
        {
            alertT -= dt;
            pose = "alert"; poseFrame = 0;
        }
        else if (chase)
        {
            idleT = 0f;
            bool right = target > catX;
            float step = Math.Min(Math.Abs(target - catX), 230f * dt);
            catX += right ? step : -step;
            animT += dt;
            pose = right ? "E" : "W";
            poseFrame = (int)(animT / 0.10f);
            dustT += dt;
            if (dustT > 0.07f)
            {
                dustT = 0f;
                dust.Add(new float[] { catX + 32 + (right ? -16 : 16), stage.Bottom - 12, 0.5f });
            }
        }
        else
        {
            idleT += dt;
            if (idleT < 1.0f) { pose = "idle"; poseFrame = 0; }
            else if (idleT < 2.5f) { pose = "scratchSelf"; poseFrame = (int)(idleT * 5f); }
            else if (idleT < 3.3f) { pose = "tired"; poseFrame = 0; }
            else { pose = "sleeping"; poseFrame = (int)(idleT / 0.6f); }
        }

        for (int i = dust.Count - 1; i >= 0; i--)
        {
            dust[i][2] -= dt;
            dust[i][1] -= 10f * dt;
            if (dust[i][2] <= 0f) dust.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------- mouse
    PointF P(MouseEventArgs e) { return new PointF(e.X / s, e.Y / s); }

    static RectangleF SliderHit(UiSlider sl)
    {
        return new RectangleF(sl.Rect.X - 8, sl.Rect.Y + 14, sl.Rect.Width + 16, 34);
    }

    void UpdateHover(PointF p)
    {
        bool any = false;
        foreach (UiTab t in tabs) { t.Hover = t.Rect.Contains(p); any |= t.Hover; }
        foreach (UiSlider sl in sliders) { sl.Hover = sl.Tab == curTab && SliderHit(sl).Contains(p); any |= sl.Hover; }
        foreach (UiToggle t in toggles) { t.Hover = t.Tab == curTab && t.Rect.Contains(p); any |= t.Hover; }
        foreach (UiSwatch w in swatches)
        {
            w.Hover = curTab == 0 && RectangleF.Inflate(w.Rect, 5, 5).Contains(p);
            any |= w.Hover;
        }
        foreach (UiButton b in buttons) { b.Hover = b.Rect.Contains(p); any |= b.Hover; }
        Cursor = any ? Cursors.Hand : Cursors.Default;
    }

    void SetFromX(UiSlider sl, float x)
    {
        float x0 = sl.Rect.X + 8, x1 = sl.Rect.Right - 8;
        float t = Clamp01((x - x0) / (x1 - x0));
        int v = sl.Min + (int)Math.Round(t * (sl.Max - sl.Min));
        if (v != sl.Get()) { sl.Set(v); Cfg.Notify(); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        PointF p = P(e);
        if (dragSlider != null) { SetFromX(dragSlider, p.X); return; }
        UpdateHover(p);
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        PointF p = P(e);
        foreach (UiButton b in buttons) if (b.Rect.Contains(p)) { b.Down = true; Capture = true; return; }
        for (int i = 0; i < tabs.Count; i++) if (tabs[i].Rect.Contains(p)) { SwitchTab(i); return; }
        foreach (UiToggle t in toggles) if (t.Tab == curTab && t.Rect.Contains(p)) { t.Flip(); return; }
        if (curTab == 0)
            foreach (UiSwatch w in swatches)
                if (RectangleF.Inflate(w.Rect, 5, 5).Contains(p))
                {
                    if (Cfg.ColorIdx != w.Index) { Cfg.ColorIdx = w.Index; Cfg.Save(); Cfg.Notify(); }
                    return;
                }
        foreach (UiSlider sl in sliders)
            if (sl.Tab == curTab && SliderHit(sl).Contains(p))
            {
                dragSlider = sl; sl.Dragging = true; Capture = true;
                SetFromX(sl, p.X);
                return;
            }
        if (p.Y < HeaderH) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); }   // drag the window
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        PointF p = P(e);
        if (dragSlider != null)
        {
            dragSlider.Dragging = false; dragSlider = null;
            Capture = false;
            Cfg.Save();
            UpdateHover(p);
        }
        UiButton fire = null;
        foreach (UiButton b in buttons)
        {
            if (b.Down && b.Rect.Contains(p)) fire = b;
            b.Down = false;
        }
        Capture = false;
        if (fire != null) fire.Click();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (dragSlider != null) return;
        foreach (UiTab t in tabs) t.Hover = false;
        foreach (UiSlider sl in sliders) sl.Hover = false;
        foreach (UiToggle t in toggles) t.Hover = false;
        foreach (UiSwatch w in swatches) w.Hover = false;
        foreach (UiButton b in buttons) b.Hover = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        PointF p = new PointF(e.X / s, e.Y / s);
        foreach (UiSlider sl in sliders)
            if (sl.Tab == curTab && SliderHit(sl).Contains(p))
            {
                int v = Math.Max(sl.Min, Math.Min(sl.Max, sl.Get() + (e.Delta > 0 ? 1 : -1)));
                if (v != sl.Get()) { sl.Set(v); Cfg.Notify(); Cfg.Save(); }
                return;
            }
    }

    // ---------------------------------------------------------- drawing helpers
    static GraphicsPath RR(RectangleF r, float rad)
    {
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    void Txt(Graphics g, string t, Font f, Color c, RectangleF r, StringAlignment h, StringAlignment v)
    {
        sfTight.Alignment = h; sfTight.LineAlignment = v;
        using (Brush b = new SolidBrush(A(c))) g.DrawString(t, f, b, r, sfTight);
    }

    float TextW(Graphics g, string t, Font f) { return g.MeasureString(t, f, 2000, sfTight).Width; }

    void Glow(Graphics g, float cx, float cy, float r, Color c, int alpha)
    {
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddEllipse(cx - r, cy - r, r * 2, r * 2);
            using (PathGradientBrush pb = new PathGradientBrush(p))
            {
                pb.CenterColor = Color.FromArgb(alpha, c);
                pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                g.FillPath(pb, p);
            }
        }
    }

    void CardBox(Graphics g, RectangleF r)
    {
        using (GraphicsPath p = RR(r, 16))
        {
            using (Brush b = new SolidBrush(A(CardC))) g.FillPath(b, p);
            using (Pen pen = new Pen(A(CardB), 1f)) g.DrawPath(pen, p);
        }
    }

    // ---------------------------------------------------------- painting
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Bg);
        g.ScaleTransform(s, s);
        ga = 1f;

        PaintHeader(g);

        // tab bar + left card + current page
        float e0 = App(0); ga = e0;
        GraphicsState st = g.Save();
        g.TranslateTransform(0, (1f - e0) * 18f);
        PaintTabBar(g);
        CardBox(g, leftCard);
        g.Restore(st);

        if (tabT < 1f)
        {
            float ez = Ease(tabT);
            float dir = curTab > prevTab ? 1f : -1f;
            PaintPage(g, prevTab, 1f - ez, -dir * 26f * ez);
            PaintPage(g, curTab, ez, dir * 26f * (1f - ez));
        }
        else PaintPage(g, curTab, 1f, 0f);

        // promo card (bottom of the window)
        float e1 = App(2); ga = e1;
        st = g.Save();
        g.TranslateTransform(0, (1f - e1) * 18f);
        CardBox(g, promoCard);
        Txt(g, "BY LAV1NRULES", fSection, Muted, new RectangleF(promoCard.X + 24, promoCard.Y + 18, 200, 16), StringAlignment.Near, StringAlignment.Near);
        using (Brush br = new SolidBrush(A(TextC)))
            g.DrawString("Like Oneko? There is more where it came from.", fDesc, br,
                         new RectangleF(promoCard.X + 24, promoCard.Y + 36, promoCard.Width - 48, 34), sfWrap);
        foreach (UiButton b in buttons) if (b.Group == 1) PaintButton(g, b);
        Txt(g, "lav1n.is-a.dev", fDesc, Muted, new RectangleF(promoCard.X, promoCard.Y + 114, promoCard.Width, 16),
            StringAlignment.Center, StringAlignment.Near);
        g.Restore(st);

        // actions + hint
        float e2 = App(1); ga = e2;
        st = g.Save();
        g.TranslateTransform(0, (1f - e2) * 18f);
        foreach (UiButton b in buttons) if (b.Group == 2) PaintButton(g, b);
        using (Brush br = new SolidBrush(A(Muted)))
            g.DrawString("Closing the window keeps the cat running in the tray. Settings save automatically.",
                         fDesc, br, new RectangleF(492, 326, 244, 50), sfWrap);
        g.Restore(st);

        ga = 1f;
        foreach (UiButton b in buttons) if (b.Group == 9) PaintWinButton(g, b);

        using (Pen pen = new Pen(Color.FromArgb(70, 80, 100), 1f))
            g.DrawRectangle(pen, 0.5f, 0.5f, W - 1f, H - 1f);
    }

    void PaintTabBar(Graphics g)
    {
        using (GraphicsPath p = RR(tabBar, 12))
        {
            using (Brush b = new SolidBrush(A(Color.FromArgb(23, 25, 38)))) g.FillPath(b, p);
            using (Pen pen = new Pen(A(CardB), 1f)) g.DrawPath(pen, p);
        }
        RectangleF ind = new RectangleF(indX + 4, tabBar.Y + 4, indW - 8, tabBar.Height - 8);
        using (GraphicsPath p = RR(ind, 9))
        using (LinearGradientBrush lg = new LinearGradientBrush(ind, A(Accent), A(Pink), 0f))
            g.FillPath(lg, p);
        foreach (UiTab t in tabs)
        {
            float sel = Clamp01(1f - Math.Abs(indX - t.Rect.X) / t.Rect.Width);
            Color c = Lerp(Lerp(Muted, TextC, t.Hov), Color.White, sel);
            Txt(g, t.Text, fBtn, c, t.Rect, StringAlignment.Center, StringAlignment.Center);
        }
    }

    void PaintPage(Graphics g, int tab, float alpha, float dx)
    {
        float e0 = App(0);
        ga = e0 * alpha;
        if (ga < 0.01f) return;
        GraphicsState st = g.Save();
        g.TranslateTransform(dx, (1f - e0) * 18f);
        Txt(g, TabTitles[tab], fSection, Muted, new RectangleF(leftCard.X + 24, leftCard.Y + 18, 300, 16),
            StringAlignment.Near, StringAlignment.Near);
        foreach (UiText t in texts)
            if (t.Tab == tab)
                using (Brush br = new SolidBrush(A(t.Accent ? Lerp(Accent, Pink, 0.5f) : Muted)))
                    g.DrawString(t.Text, fDesc, br, t.Rect, sfWrap);
        foreach (UiSlider sl in sliders) if (sl.Tab == tab) PaintSlider(g, sl);
        foreach (UiToggle t in toggles) if (t.Tab == tab) PaintToggle(g, t);
        if (tab == 0) PaintSwatches(g);
        g.Restore(st);
        ga = 1f;
    }

    void PaintHeader(Graphics g)
    {
        RectangleF hr = new RectangleF(0, 0, W, HeaderH);
        GraphicsState st = g.Save();
        g.SetClip(hr, CombineMode.Intersect);
        using (LinearGradientBrush lg = new LinearGradientBrush(hr, Color.FromArgb(34, 28, 88), Color.FromArgb(98, 46, 138), 15f))
            g.FillRectangle(lg, hr);
        Glow(g, 130 + 60 * (float)Math.Sin(clock * 0.5), 46 + 18 * (float)Math.Cos(clock * 0.4), 190, Pink, 85);
        Glow(g, 560 + 70 * (float)Math.Cos(clock * 0.35), 96 + 18 * (float)Math.Sin(clock * 0.6), 220, Color.FromArgb(100, 140, 255), 80);

        float te = Ease(intro / 0.5f);
        ga = te;
        GraphicsState st2 = g.Save();
        g.TranslateTransform((1f - te) * -16f, 0);
        Txt(g, "Oneko for Windows", fTitle, Color.White, new RectangleF(32, 34, 400, 40), StringAlignment.Near, StringAlignment.Near);
        Txt(g, "by Lav1nRules", fSub, Color.FromArgb(255, 190, 224), new RectangleF(34, 80, 300, 22), StringAlignment.Near, StringAlignment.Near);
        g.Restore(st2);
        ga = 1f;

        PaintStage(g);
        g.Restore(st);
    }

    void PaintStage(Graphics g)
    {
        float se = Ease((intro - 0.1f) / 0.5f);
        ga = se;
        using (GraphicsPath p = RR(stage, 16))
        {
            using (Brush b = new SolidBrush(A(Color.FromArgb(80, 8, 8, 30)))) g.FillPath(b, p);
            using (Pen pen = new Pen(A(Color.FromArgb(46, 255, 255, 255)), 1f)) g.DrawPath(pen, p);

            GraphicsState st = g.Save();
            g.SetClip(p, CombineMode.Intersect);

            for (int i = 0; i < starX.Length; i++)
            {
                float tw = 0.35f + 0.35f * (float)Math.Sin(clock * 2f + starP[i]);
                using (Brush b = new SolidBrush(A(Color.FromArgb((int)(255 * tw), 255, 255, 255))))
                    g.FillEllipse(b, starX[i] - 1.2f, starY[i] - 1.2f, 2.4f, 2.4f);
            }
            float ground = stage.Bottom - 10;
            using (Pen pen = new Pen(A(Color.FromArgb(60, 255, 255, 255)), 1.5f))
                g.DrawLine(pen, stage.Left + 12, ground, stage.Right - 12, ground);

            foreach (float[] d in dust)
            {
                float life = d[2] / 0.5f;
                float rad = 1.2f + 2.6f * life;
                using (Brush b = new SolidBrush(A(Color.FromArgb((int)(150 * life), 255, 255, 255))))
                    g.FillEllipse(b, d[0] - rad, d[1] - rad, rad * 2, rad * 2);
            }

            // terminal prompt shown while previewing the Terminal Version
            if (stageTerm > 0.02f)
            {
                string cursor = ((int)(clock * 2.2f) % 2 == 0) ? "_" : " ";
                float save = ga; ga = save * stageTerm;
                Txt(g, "C:\\> oneko" + cursor, fMono, Color.FromArgb(90, 255, 140),
                    new RectangleF(stage.X + 14, stage.Y + 10, 160, 16), StringAlignment.Near, StringAlignment.Near);
                ga = save;
            }

            int block = 1 + (int)Math.Round(stageTerm * (Cfg.Pixel - 1));
            bool tint = Cfg.TermTint && stageTerm > 0.5f;
            Rectangle srcR = Sprites.Src(pose, poseFrame);
            RectangleF dest = new RectangleF((float)Math.Round(catX), ground - 64 + 5, 64, 64);
            Sprites.DrawPix(g, dest, srcR, block, tint);
            g.Restore(st);
        }
        if (Cfg.Paused)
            Txt(g, "paused", fDesc, Color.FromArgb(255, 200, 220), new RectangleF(stage.Right - 80, stage.Y + 10, 66, 16),
                StringAlignment.Far, StringAlignment.Near);
        ga = 1f;
    }

    void PaintSwatches(Graphics g)
    {
        float x = leftCard.X + 24, y = leftCard.Y + 50 + 186;
        Txt(g, "Cat color", fLabel, TextC, new RectangleF(x, y, 200, 20), StringAlignment.Near, StringAlignment.Near);
        Txt(g, Sprites.ColorNames[Cfg.ColorIdx], fValue, Lerp(Accent, Pink, 0.5f),
            new RectangleF(leftCard.Right - 24 - 140, y, 140, 20), StringAlignment.Far, StringAlignment.Near);
        foreach (UiSwatch w in swatches)
        {
            float cx = w.Rect.X + w.Rect.Width / 2, cy = w.Rect.Y + w.Rect.Height / 2;
            float r = 13f + 1.5f * w.Hov;
            if (w.Sel > 0.01f)
            {
                float rr = r + 4.5f;
                using (Pen pen = new Pen(A(Color.FromArgb((int)(255 * w.Sel), Lerp(Accent, Pink, 0.5f))), 2f))
                    g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2, rr * 2);
            }
            using (Brush b = new SolidBrush(A(Sprites.SwatchOutline(w.Index))))
                g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
            using (Brush b = new SolidBrush(A(Sprites.SwatchFill(w.Index))))
                g.FillEllipse(b, cx - r + 2.5f, cy - r + 2.5f, (r - 2.5f) * 2, (r - 2.5f) * 2);
        }
    }

    void PaintSlider(Graphics g, UiSlider sl)
    {
        RectangleF r = sl.Rect;
        Txt(g, sl.Title, fLabel, TextC, new RectangleF(r.X, r.Y, 200, 20), StringAlignment.Near, StringAlignment.Near);
        float tw = TextW(g, sl.Title, fLabel);
        Txt(g, sl.Desc, fDesc, Muted, new RectangleF(r.X + tw + 10, r.Y + 2, 220, 18), StringAlignment.Near, StringAlignment.Near);
        Txt(g, sl.Fmt((int)Math.Round(sl.Disp)), fValue, Lerp(Accent, Pink, 0.5f),
            new RectangleF(r.Right - 110, r.Y, 110, 20), StringAlignment.Far, StringAlignment.Near);

        float x0 = r.X + 8, x1 = r.Right - 8, cy = r.Y + 34;
        float t = sl.Max > sl.Min ? Clamp01((sl.Disp - sl.Min) / (sl.Max - sl.Min)) : 0f;
        float kx = x0 + (x1 - x0) * t;

        using (GraphicsPath track = RR(new RectangleF(x0, cy - 3, x1 - x0, 6), 3))
        using (Brush b = new SolidBrush(A(TrackC))) g.FillPath(b, track);

        if (kx - x0 > 2f)
            using (GraphicsPath fill = RR(new RectangleF(x0, cy - 3, kx - x0, 6), 3))
            using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(x0, cy - 3, x1 - x0, 6), A(Accent), A(Pink), 0f))
                g.FillPath(lg, fill);

        float act = Math.Min(1f, sl.Hov + sl.Drag);
        float rad = 8f + 1.5f * sl.Hov + 1.5f * sl.Drag;
        if (act > 0.01f)
            using (Brush b = new SolidBrush(A(Color.FromArgb((int)(60 * act), Accent))))
                g.FillEllipse(b, kx - rad - 7, cy - rad - 7, (rad + 7) * 2, (rad + 7) * 2);
        using (Brush b = new SolidBrush(A(Color.FromArgb(70, 0, 0, 0))))
            g.FillEllipse(b, kx - rad, cy - rad + 1.5f, rad * 2, rad * 2);
        using (Brush b = new SolidBrush(A(Color.White)))
            g.FillEllipse(b, kx - rad, cy - rad, rad * 2, rad * 2);
        using (Brush b = new SolidBrush(A(Lerp(Accent, Pink, t))))
            g.FillEllipse(b, kx - 3.5f, cy - 3.5f, 7, 7);

        if (sl.Drag > 0.02f)
        {
            string vt = sl.Fmt((int)Math.Round(sl.Disp));
            float bw = Math.Max(34f, TextW(g, vt, fValue) + 18f);
            float by = cy - rad - 32 + (1f - sl.Drag) * 8f;
            RectangleF br = new RectangleF(kx - bw / 2, by, bw, 24);
            float save = ga; ga = save * sl.Drag;
            using (GraphicsPath p = RR(br, 8))
            using (Brush b = new SolidBrush(A(Accent))) g.FillPath(b, p);
            PointF[] tri = { new PointF(kx - 5, br.Bottom - 1), new PointF(kx + 5, br.Bottom - 1), new PointF(kx, br.Bottom + 5) };
            using (Brush b = new SolidBrush(A(Accent))) g.FillPolygon(b, tri);
            Txt(g, vt, fValue, Color.White, br, StringAlignment.Center, StringAlignment.Center);
            ga = save;
        }
    }

    void PaintToggle(Graphics g, UiToggle t)
    {
        RectangleF r = t.Rect;
        Color titleC = Lerp(TextC, Color.White, t.Hov);
        Txt(g, t.Title, fLabel, titleC, new RectangleF(r.X, r.Y, r.Width - 56, 20), StringAlignment.Near, StringAlignment.Near);
        Txt(g, t.Desc, fDesc, Muted, new RectangleF(r.X, r.Y + 19, r.Width - 56, 18), StringAlignment.Near, StringAlignment.Near);

        RectangleF pill = new RectangleF(r.Right - 44, r.Y + 6, 44, 24);
        Color track = Lerp(TrackC, Accent, t.On);
        using (GraphicsPath p = RR(pill, 12))
        {
            using (Brush b = new SolidBrush(A(track))) g.FillPath(b, p);
            if (t.Hov > 0.01f)
                using (Pen pen = new Pen(A(Color.FromArgb((int)(120 * t.Hov), Accent)), 1.5f)) g.DrawPath(pen, p);
        }
        float kx = pill.X + 12 + 20 * t.On;
        using (Brush b = new SolidBrush(A(Color.FromArgb(70, 0, 0, 0))))
            g.FillEllipse(b, kx - 9, pill.Y + 12 - 9 + 1.2f, 18, 18);
        using (Brush b = new SolidBrush(A(Color.White)))
            g.FillEllipse(b, kx - 9, pill.Y + 12 - 9, 18, 18);
    }

    void PaintButton(Graphics g, UiButton b)
    {
        RectangleF r = b.Rect;
        GraphicsState st = g.Save();
        float sc = 1f - 0.04f * b.Press;
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        g.TranslateTransform(cx, cy);
        g.ScaleTransform(sc, sc);
        g.TranslateTransform(-cx, -cy);

        string text = b.TextFn != null ? b.TextFn() : b.Text;
        Color tc = TextC;
        using (GraphicsPath p = RR(r, 11))
        {
            if (b.Style == 0 || b.Style == 5)
            {
                Color a1 = b.Style == 0 ? Accent : Cyan, a2 = b.Style == 0 ? Pink : Accent;
                Color c1 = Lerp(a1, Color.White, 0.18f * b.Hov);
                Color c2 = Lerp(a2, Color.White, 0.18f * b.Hov);
                using (LinearGradientBrush lg = new LinearGradientBrush(r, A(c1), A(c2), 0f)) g.FillPath(lg, p);
                tc = Color.White;
            }
            else if (b.Style == 1)
            {
                using (Brush br = new SolidBrush(A(Lerp(Color.FromArgb(36, 39, 60), Color.FromArgb(52, 56, 86), b.Hov)))) g.FillPath(br, p);
                using (Pen pen = new Pen(A(Lerp(CardB, Accent, b.Hov * 0.7f)), 1f)) g.DrawPath(pen, p);
            }
            else
            {
                using (Brush br = new SolidBrush(A(Color.FromArgb((int)(70 * b.Hov), Danger)))) g.FillPath(br, p);
                using (Pen pen = new Pen(A(Color.FromArgb((int)(120 + 100 * b.Hov), Danger)), 1f)) g.DrawPath(pen, p);
                tc = Danger;
            }
        }
        if (b.Style == 5)
        {
            // text + a little "open link" arrow that nudges on hover
            float w = TextW(g, text, fBtn);
            float x0 = cx - (w + 18) / 2;
            Txt(g, text, fBtn, tc, new RectangleF(x0, r.Y, w + 2, r.Height), StringAlignment.Near, StringAlignment.Center);
            float ax = x0 + w + 8 + 2f * b.Hov, ay = cy + 1 - 1.5f * b.Hov;
            using (Pen pen = new Pen(A(Color.White), 1.8f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawLine(pen, ax, ay + 4, ax + 8, ay - 4);
                g.DrawLine(pen, ax + 2, ay - 4, ax + 8, ay - 4);
                g.DrawLine(pen, ax + 8, ay - 4, ax + 8, ay + 2);
            }
        }
        else Txt(g, text, fBtn, tc, r, StringAlignment.Center, StringAlignment.Center);
        g.Restore(st);
    }

    void PaintWinButton(Graphics g, UiButton b)
    {
        RectangleF r = b.Rect;
        if (b.Hov > 0.01f)
        {
            Color c = b.Style == 4 ? Color.FromArgb((int)(210 * b.Hov), 232, 70, 90) : Color.FromArgb((int)(45 * b.Hov), 255, 255, 255);
            using (Brush br = new SolidBrush(c))
            {
                if (b.Style == 4) g.FillRectangle(br, r.X, r.Y + 1, r.Width - 1, r.Height - 1);
                else g.FillRectangle(br, r);
            }
        }
        Color ic = Lerp(Color.FromArgb(200, 255, 255, 255), Color.White, b.Hov);
        using (Pen pen = new Pen(ic, 1.4f))
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            if (b.Style == 3) g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
            else { g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5); g.DrawLine(pen, cx - 5, cy + 5, cx + 5, cy - 5); }
        }
    }
}

// ---------------------------------------------------------------- app
class Ctx : ApplicationContext
{
    NotifyIcon tray;
    CatForm cat;
    SettingsForm settings;
    bool balloonShown;
    Control marshal = new Control();

    public Ctx(bool startHidden)
    {
        IntPtr unused = marshal.Handle;   // force handle creation so other threads can post to the UI thread
        Icon icon = Sprites.MakeIcon();
        cat = new CatForm();
        settings = new SettingsForm(icon);
        settings.HiddenToTray += delegate
        {
            if (balloonShown) return;
            balloonShown = true;
            tray.ShowBalloonTip(3000, "Oneko is still running",
                "Double-click the cat icon to reopen settings.", ToolTipIcon.Info);
        };

        tray = new NotifyIcon();
        tray.Icon = icon;
        tray.Text = "Oneko for Windows";
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem open = new ToolStripMenuItem("Open settings");
        ToolStripMenuItem pause = new ToolStripMenuItem("Pause cat");
        ToolStripMenuItem more = new ToolStripMenuItem("See more creations");
        ToolStripMenuItem quit = new ToolStripMenuItem("Quit Oneko");
        open.Click += delegate { settings.ShowSettings(); };
        pause.Click += delegate { Cfg.Paused = !Cfg.Paused; Cfg.Notify(); };
        more.Click += delegate { Links.OpenCreations(); };
        quit.Click += delegate { Application.Exit(); };
        menu.Items.Add(open);
        menu.Items.Add(pause);
        menu.Items.Add(more);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quit);
        menu.Opening += delegate { pause.Text = Cfg.Paused ? "Resume cat" : "Pause cat"; };
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { settings.ShowSettings(); };
        tray.Visible = true;

        cat.Show();
        if (!startHidden && Cfg.OpenOnLaunch) settings.ShowSettings();
    }

    // called from the "someone launched Oneko again" watcher thread
    public void RequestShow()
    {
        try { marshal.BeginInvoke((MethodInvoker)delegate { settings.ShowSettings(); }); }
        catch { }
    }

    public void Cleanup()
    {
        tray.Visible = false;
        tray.Dispose();
    }
}

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);

    [STAThread]
    static void Main(string[] args)
    {
        bool created;
        System.Threading.Mutex mtx = new System.Threading.Mutex(true, "Oneko.Lav1nRules.Singleton", out created);
        System.Threading.EventWaitHandle showEvt =
            new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "Oneko.Lav1nRules.ShowSettings");
        if (!created)
        {
            // already running: tell the running copy to open its settings window, then quit quietly
            try { AllowSetForegroundWindow(-1); } catch { }
            showEvt.Set();
            return;
        }

        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); }   // per-monitor aware: coordinates stay correct across several monitors
        catch { try { SetProcessDPIAware(); } catch { } }
        Application.EnableVisualStyles();
        Sprites.Load();
        Cfg.Load();
        Sprites.ApplyColor(Cfg.ColorIdx);

        Ctx ctx = new Ctx(Array.IndexOf(args, "--tray") >= 0);
        System.Threading.Thread watcher = new System.Threading.Thread(delegate ()
        {
            while (true) { showEvt.WaitOne(); ctx.RequestShow(); }
        });
        watcher.IsBackground = true;
        watcher.Start();

        Application.Run(ctx);
        ctx.Cleanup();
        GC.KeepAlive(mtx);
    }
}
