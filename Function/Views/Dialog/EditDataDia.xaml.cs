using Function.Models;
using Function.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Function.Views.Pages
{
    /// <summary>
    /// EditData.xaml 的交互逻辑
    /// </summary>
    public partial class EditDataDia
    {
        // 公开一个属性，供主窗口获取最终的数据
        public IpInfoConfig ResultDataIp { get; private set; }
        public RomoteInfo ResultDataRem { get; private set; }
        public RomoteFile ResultDataFile { get; private set; }

        //1:ip增加 2:ip编辑 3:远程增加 4:远程编辑 5:远程文件增加 6:远程文件编辑
        public int _mode { get; private set; }
        // 构造函数：参数为 null 代表是【添加】，不为 null 代表是【编辑】
        public EditDataDia(int mode,object existingData = null)
        {
            InitializeComponent();
            _mode = mode;

            if (mode == 1)
            {
                ResultDataIp = new IpInfoConfig();
                TitleTextBlock.Text = "添加";
                return;
            }
            else if(_mode==3)
            {
                ResultDataRem = new RomoteInfo();
                TitleTextBlock.Text = "添加(当用户名为CX9020会只有Beckhoff的远程桌面)";
                TitleTextBlock.FontTypography = FontTypography.BodyStrong;
                TxtSubnet.Text = "Admin";
                TxtSubnet.PlaceholderText = "远程的用户名";
                TxtGateway.Text = "123456";
                TxtGateway.PlaceholderText = "远程的密码";
                return;
            }
            else if (_mode == 5)
            {
                ResultDataFile = new RomoteFile();
                TitleTextBlock.Text = "添加";
                TitleTextBlock.FontTypography = FontTypography.BodyStrong;
                TxtSubnet.Text = "Admin";
                TxtSubnet.PlaceholderText = "远程的用户名";
                TxtGateway.Text = "123456";
                TxtGateway.PlaceholderText = "远程的密码";
                return;
            }
            else
            {
                TitleTextBlock.Text = "编辑";
            }

            if (existingData != null)
            {
                if (existingData is IpInfoConfig cfg)
                {
                    // === 编辑模式：回显数据 ===
                    TxtIp.Text = cfg.Ip;
                    TxtSubnet.Text = cfg.SubNet;
                    TxtRemarks.Text = cfg.remark;

                    // 为了避免修改原对象（万一用户点了取消），我们不直接操作 existingData
                    // 而是把 ID 存下来，或者最后生成一个新的对象
                    ResultDataIp = cfg;
                }
                else if (existingData is RomoteInfo rem)
                {
                    // === 编辑模式：回显数据 ===
                    TxtIp.Text = rem.Ip;
                    TxtSubnet.Text = rem.UserName;
                    TxtGateway.Text = rem.PassWord;
                    TxtRemarks.Text = rem.remark;

                    // 为了避免修改原对象（万一用户点了取消），我们不直接操作 existingData
                    // 而是把 ID 存下来，或者最后生成一个新的对象
                    ResultDataRem = rem;
                }
                else if (existingData is RomoteFile Rfile)
                {
                    // === 编辑模式：回显数据 ===
                    TxtIp.Text = Rfile.Ip;
                    TxtSubnet.Text = Rfile.UserName;
                    TxtGateway.Text = Rfile.PassWord;
                    TxtRemarks.Text = Rfile.remark;

                    // 为了避免修改原对象（万一用户点了取消），我们不直接操作 existingData
                    // 而是把 ID 存下来，或者最后生成一个新的对象
                    ResultDataFile = Rfile;
                }


            }
            
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. 获取输入值
            string ipInput = TxtIp.Text.Trim();
            string subnetInput = TxtSubnet.Text.Trim();
            string remarkInput = TxtRemarks.Text.Trim();
            string getWayInput = TxtGateway.Text.Trim();
            // 2. 校验 IP 格式
            bool isValid = IPAddress.TryParse(ipInput, out IPAddress address)
                 && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

            if (!isValid)
            {
                System.Windows.MessageBox.Show("IP 地址格式不正确 (例如: 192.168.1.1)", "格式错误");
                return; // 阻止保存
            }
            if (_mode <= 2)
            {
                // 3. 校验 子网掩码 (可选，逻辑同上)
                isValid = IPAddress.TryParse(subnetInput, out address)
                     && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

                if (!isValid)
                {
                    System.Windows.MessageBox.Show("IP 地址格式不正确 (例如: 192.168.1.1)", "格式错误");
                    return; // 阻止保存
                }
                // 4. 将数据写入结果对象
                ResultDataIp.Ip = ipInput;
                ResultDataIp.SubNet = subnetInput;
                ResultDataIp.remark = remarkInput;
                ResultDataIp.GetWay = getWayInput;

            }
            else if(_mode<=4)
            {
                // 3. 校验 用户名 (可选，逻辑同上)
                if (string.IsNullOrEmpty(subnetInput))
                {
                    System.Windows.MessageBox.Show("用户名不能为空", "格式错误");
                    return; // 阻止保存
                }
                ResultDataRem.Ip = ipInput;
                ResultDataRem.UserName = subnetInput;
                ResultDataRem.remark = remarkInput;
                ResultDataRem.PassWord = getWayInput;
            }
            else if (_mode <= 6)
            {
                // 3. 校验 用户名 (可选，逻辑同上)
                if (string.IsNullOrEmpty(subnetInput))
                {
                    System.Windows.MessageBox.Show("用户名不能为空", "格式错误");
                    return; // 阻止保存
                }
                ResultDataFile.Ip = ipInput;
                ResultDataFile.UserName = subnetInput;
                ResultDataFile.remark = remarkInput;
                ResultDataFile.PassWord = getWayInput;
            }


            // 5. 设置 DialogResult 为 true 并关闭
            this.DialogResult = true;
            this.Close();


        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
