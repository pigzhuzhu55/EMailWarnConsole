using Cly.Common.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EMailWarnConsole
{
    class Program
    {
        static int CaptionLen = 10240;
        static void Main(string[] args)
        {
            //初始化配置信息
            List<Site> sites = new List<Site>();
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"config.xml");
            var root = XDocument.Load(configPath);

            Dictionary<string, User> userDics = new Dictionary<string, User>();
            foreach (XElement e in root.Element("root").Element("users").Elements("user"))
            {
                User user = new User();
                user.Name = e.Element("name").Value.Trim();
                user.Mail = e.Element("mail").Value.Trim();
                user.QQ = e.Element("qq").Value.Trim();

                userDics[user.Name] = user;
            }

            foreach (XElement e in root.Element("root").Element("sites").Elements("site"))
            {
                Site site = new Site();
                site.Name = e.Element("name").Value.Trim();
                site.Path = e.Element("path").Value.Trim();
                site.WatchList = new Dictionary<string, FileEx>();
                site.UserList = new List<User>();

                foreach (string filename in e.Element("watch").Value.Trim().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    site.WatchList[filename] = new FileEx();
                }

                Console.WriteLine($"加入监控站点:{site.Name}，监控目录:{e.Element("watch").Value.Trim()}");
                foreach (string username in e.Element("user").Value.Trim().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    User user = null;
                    if (userDics.TryGetValue(username.Trim(), out user))
                    {
                        Console.WriteLine($"监控人员:{username}");
                        site.UserList.Add(user);
                    }
                }
                sites.Add(site);
            }

            Console.WriteLine($"初始化完毕，准备开始监控...");
            Thread.Sleep(1000);
            Console.WriteLine("3");
            Thread.Sleep(1000);
            Console.WriteLine("2");
            Thread.Sleep(1000);
            Console.WriteLine("1");

            while (true)
            {
                try
                {
                    foreach (Site site in sites)
                    {
                        foreach (var watch in site.WatchList)
                        {
                            string watchPath = Path.Combine(site.Path, watch.Key);
                            FileEx fx = watch.Value;
                            FileEx fxNow = GetLatestFileTimeInfo(watchPath);
                            if (fxNow != null)
                            {
                                if (fx.LastWriteTime < fxNow.LastWriteTime)
                                {
                                    fx.FileName = fxNow.FileName;
                                    fx.Content = fxNow.Content;
                                    fx.Length = fxNow.Length;
                                    fx.LastWriteTime = fxNow.LastWriteTime;
                                }
                                else
                                {
                                    fx.LastNotifyTime = fxNow.LastWriteTime;
                                }

                                if (fx.LastNotifyTime < fx.LastWriteTime)
                                {
                                    if (fx.LastNotifyTime != DateTimeHelper.BaseTime)
                                    {
                                        Console.WriteLine($"{DateTime.Now.ToLongTimeString()}:站点{site.Name}的目录{watch.Key}发送监控异常邮件");
                                        SendMailUse($"站点{site.Name}的目录{watch.Key}的最后一条信息", "监控短信", fx.Content, site.UserList);
                                    }

                                    fx.LastNotifyTime = fx.LastWriteTime;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now.ToLongTimeString()}:{ex.ToString()}");
                }
                finally
                {
                    Thread.Sleep(1000 * 30);
                }
            }
        }
        /// <summary>
        /// 获取文件夹里面最新的一个文件
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="ext"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns></returns>
        static FileEx GetLatestFileTimeInfo(string dir, string ext = ".txt")
        {
            if(!System.IO.Directory.Exists(dir))
            {
                return null;
            }

            List<FileEx> list = new List<FileEx>();
            DirectoryInfo d = new DirectoryInfo(dir);
            foreach (FileInfo fi in d.GetFiles())
            {
                if (fi.Extension.ToUpper() == ext.ToUpper())
                {
                    list.Add(new FileEx()
                    {
                        FileName = fi.FullName,
                        LastWriteTime = fi.LastWriteTime,
                        Length = (int)(fi.Length % int.MaxValue)
                    });
                }
            }
            var qry = (from x in list
                       orderby x.LastWriteTime
                       select x).LastOrDefault();
            if (qry != null && qry.Length > 0)
            {
                int start = 0;
                int len = qry.Length;
                if (len > CaptionLen)
                {
                    start = len - CaptionLen;
                    len = CaptionLen;
                }
                byte[] arr = new byte[CaptionLen];

                using (var fs = File.OpenRead(qry.FileName))
                {
                    fs.Position = start;
                    fs.Read(arr, 0, len);
                }
                var str = Encoding.UTF8.GetString(arr);

                string pattern = @"(?<=[\=]{28,50})";
                int matchLast = 0;
                foreach (Match match in Regex.Matches(str, pattern))
                {
                    matchLast = match.Index;
                }
                qry.Content = str.Substring(matchLast);
            }
            return qry;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="xyf"></param>
        /// <param name="content"></param>
        /// <param name="users"></param>
        public static void SendMailUse(string title, string xyf, string content,List<User> users)
        {
            //发送邮箱我用的是网易邮箱，可以自学修改
            string host = "smtp.163.com";// 邮件服务器smtp.163.com表示网易邮箱服务器
            string userName = "xxx@163.com";// 发送端账号--这里填写发送者的邮箱账号
            string password = "xxx";// 发送端密码(这个客户端重置后的密码)

            SmtpClient client = new SmtpClient();
            client.DeliveryMethod = SmtpDeliveryMethod.Network;//指定电子邮件发送方式    
            client.Host = host;//邮件服务器
            client.UseDefaultCredentials = true;
            client.Credentials = new System.Net.NetworkCredential(userName, password);//用户名、密码

            //////////////////////////////////////
            string strfrom = userName;

            string subject = title;//邮件的主题             
            string body = content;//发送的邮件正文  

            System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();
            msg.From = new MailAddress(strfrom, xyf);

            if (users != null)
            {
                foreach (var user in users)
                {
                    msg.To.Add(user.Mail); msg.CC.Add(user.QQ+"@qq.com");
                }
            }

            msg.Subject = subject;//邮件标题   
            msg.Body = body;//邮件内容   
            msg.BodyEncoding = System.Text.Encoding.UTF8;//邮件内容编码   
            msg.IsBodyHtml = true;//是否是HTML邮件   
            msg.Priority = MailPriority.High;//邮件优先级   


            try
            {
                client.Send(msg);
                Console.WriteLine("发送成功");
            }
            catch (System.Net.Mail.SmtpException ex)
            {
                Console.WriteLine(ex.Message, "发送邮件出错");
            }
        }

        public class FileEx
        { 
            public FileEx()
            {
                LastNotifyTime = DateTimeHelper.BaseTime;
                LastWriteTime = DateTimeHelper.BaseTime;
                Content = string.Empty;
            }
            public string FileName { get; set; }
            public string Content { get; set; }
            public int Length { get; set; }
            public DateTime LastWriteTime { get; set; }
            /// <summary>
            /// 短信通知的异常时间（写入异常文件的时间）
            /// </summary>
            public DateTime LastNotifyTime { get; set; }
        }
        public class User
        {
            public string Name { get; set; }
            public string Mail { get; set; }
            public string QQ { get; set; }
        }
        public class Site
        {
            public string Name { get; set; }
            public string Path { get; set; }
            /// <summary>
            /// 需监控的文件夹列表
            /// </summary>
            public Dictionary<string, FileEx> WatchList { get; set; }
            public List<User> UserList { get; set; }
        }
    }
}
