using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace BarcodeForm
{
    public partial class Form1 : Form
    {
        private LowLevelKeyboardListener _listener;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _listener = new LowLevelKeyboardListener(this);
            _listener.OnKeyPressed += _listener_OnKeyPressed;

            _listener.HookKeyboard();
        }

        

        void _listener_OnKeyPressed(object sender, KeyPressedArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                this.label1.Text = e.KeyPressed.ToString();
            });
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield return (T)Enumerable.Empty<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject ithChild = VisualTreeHelper.GetChild(depObj, i);
                if (ithChild == null) continue;
                if (ithChild is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(ithChild)) yield return childOfChild;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _listener.UnHookKeyboard();
        }
    }


    public class LowLevelKeyboardListener
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public event EventHandler<KeyPressedArgs> OnKeyPressed;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        DateTime _lastKeystroke = new DateTime(0);
        List<string> _barcode = new List<string>();

        TimerExample timerExample;

        public Form Main { get; }

        public LowLevelKeyboardListener(Form main)
        {
            _proc = HookCallback;

            timerExample = new TimerExample();
            timerExample.TimeCrossedSpecificTime += Invoke;
            timerExample.ReRun();
            Main = main;
        }

        public void HookKeyboard()
        {
            _hookID = SetHook(_proc);

            _lastKeystroke = new DateTime(0);
            _barcode = new List<string>();
        }

        public void UnHookKeyboard()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var key = KeyInterop.KeyFromVirtualKey(vkCode);

                if (_barcode.Count < 1)
                    timerExample.ReRun();

                if (Keys.IsNumber.Any(x => x == (int)key))
                    _barcode.Add(key.ToString().Replace("D", ""));

                if (Keys.IsAlpha.Any(x => x == (int)key))
                    _barcode.Add(key.ToString());

                if ((int)key == 84)
                    _barcode.Add("*");

                if ((int)key == 87)
                    _barcode.Add("-");


                if (key == Key.Enter && _barcode.Count > 9)
                    Invoke();

            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }


        void Invoke(object sender = null, EventArgs e = null)
        {
            if (_barcode.Count < 9)
            {
                _barcode.Clear();
                return;
            }



            var f = string.Join("", _barcode.Select(x => x.Replace("D", ""))) + Environment.NewLine;
            _barcode.Clear();
            //f += Environment.NewLine;
            if (OnKeyPressed != null)
            {
                OnKeyPressed(this, new KeyPressedArgs(f));

                Task.Delay(new TimeSpan(0, 0, 0, 0, 40)).ContinueWith(o =>
                {
                    Main.Invoke((MethodInvoker)delegate
                    {
                        ControlTyping(f);
                    });
                });
                
            }
        }

        void ControlTyping(string txt = "")
        {
            if (txt.Length < 1)
                return;
            var Focusedcontrol = FindFocusedControl(Main);
            var barcode = txt.Replace("\r", "").Replace("\n", "");
            if (Focusedcontrol is TextBox)
            {
                var textBox = (TextBox)Focusedcontrol;
                textBox.Text = textBox.Text.Replace(barcode, "");
                textBox.Select(textBox.Text.Length, 0);
            }
            if (Focusedcontrol is ComboBox)
            {
                var comboBox = (ComboBox)Focusedcontrol;
                comboBox.Text = comboBox.Text.Replace(barcode, "");
                comboBox.Select(comboBox.Text.Length, 0);
            }
        }

        Control FindFocusedControl(Control control)
        {
            var container = control as ContainerControl;
            return (null != container
                ? FindFocusedControl(container.ActiveControl)
                : control);
        }
    }

    public static class Ext
    {
        public static void SetText(this RichTextBox richTextBox, string text)
        {
            richTextBox.Text = text;
        }

        public static string GetText(this RichTextBox richTextBox)
        {
            return richTextBox.Text;
        }
    }

    public class KeyPressedArgs : EventArgs
    {
        public string KeyPressed { get; private set; }

        public KeyPressedArgs(string key)
        {
            KeyPressed = key;
        }
    }


    public class Keys
    {
        //public static List<int> IsValid = IsNumber.Concat(IsAlpha).ToList();
        public static List<int> IsNumber = new List<int>
        {
            //numbers
            34,
            35,
            36,
            37,
            38,
            39,
            40,
            41,
            42,
            43,
        };
        public static List<int> IsAlpha = new List<int>
        {
            //Alpha
            44,
            45,
            46,
            47,
            48,
            49,
            50,
            51,
            52,
            53,
            54,
            55,
            56,
            57,
            58,
            59,
            60,
            61,
            62,
            63,
            64,
            65,
            66,
            67,
            68,
            69,
        };
    }


    public class TimerExample
    {
        public event EventHandler TimeCrossedSpecificTime;
        public static System.Timers.Timer timer = new System.Timers.Timer(1);
        private DateTime specificTime;

        public TimerExample()
        {
            timer.Elapsed += TimerElapsed;
            ReRun();
        }
        public void ReRun()
        {
            specificTime = DateTime.Now.AddMilliseconds(100);
            timer.Interval = 1;
            timer.Start();
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now >= specificTime)
            {
                timer.Stop();
                OnTimeCrossedSpecificTime(EventArgs.Empty);
            }
        }

        protected virtual void OnTimeCrossedSpecificTime(EventArgs e)
        {
            TimeCrossedSpecificTime?.Invoke(this, e);
        }
    }
}
