using CommunityToolkit.Mvvm.DependencyInjection; // 重点引入 IoC
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Function.Helpers
{
   
        public static class GlobalHotkeyManager
        {
            // 核心字典：用来存储 全局热键ID -> 对应的执行动作
            private static readonly Dictionary<string, Action> _actionRegistry = new();

            /// <summary>
            /// 1. 任意页面调用此方法，将自己的方法（甚至带参数的方法）“托管”进字典中
            /// </summary>
            /// <param name="hotkeyId">全程序唯一的热键标识符</param>
            /// <param name="action">要执行的方法委托</param>
            public static void RegisterAction(string hotkeyId, Action action)
            {
                _actionRegistry[hotkeyId] = action;
            }

            /// <summary>
            /// 2. 设置页面调用此方法，真正向 Windows 系统注册或修改按键
            /// </summary>
            /// <param name="hotkeyId">全程序唯一的热键标识符</param>
            public static void BindOrUpdateHotkey(string hotkeyId, ModifierKeys modifiers, Key key)
            {
                try
                {
                    // 注意最后一个参数：所有热键触发时，统一进入 OnGlobalHotkeyTriggered
                    HotkeyManager.Current.AddOrReplace(hotkeyId, key, modifiers, OnGlobalHotkeyTriggered);
                }
                catch (HotkeyAlreadyRegisteredException)
                {
                    System.Windows.MessageBox.Show($"热键被占用，请更换！");
                }
            }

            /// <summary>
            /// 3. 全局统一拦截事件：操作系统触发热键时，由此方法负责动态分发
            /// </summary>
            private static void OnGlobalHotkeyTriggered(object? sender, HotkeyEventArgs e)
            {
                e.Handled = true; // 拦截系统按键

                // NHotkey 的精华：e.Name 就是我们在 AddOrReplace 时传入的 hotkeyId！
                // 去字典里找这个 ID 对应的方法，如果找到了，就执行它
                if (_actionRegistry.TryGetValue(e.Name, out var actionToExecute))
                {
                    actionToExecute?.Invoke();
                }
            }

        }
    
}
