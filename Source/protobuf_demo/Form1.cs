using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace protobuf_demo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var person = new Person
            {
                Name="lx",
                Age=10,
                Hobbies = { "打球","游泳","跑步"}
            };
            byte[] data = person.ToByteArray();
            // 将字节数组反序列化为 Person 对象
            Person deserializedPerson = Person.Parser.ParseFrom(data);
            using (FileStream fs = new FileStream("person.bin", FileMode.Create, FileAccess.Write))
            {
                fs.Write(data, 0, data.Length);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //Person newPerson;
            //using (var file = File.OpenRead("person.bin"))
            //{
            //    newPerson = Serializer.Deserialize<Person>(file);
            //}
            using (FileStream fs = new FileStream("person.bin", FileMode.Open, FileAccess.Read))
            {
                // 创建一个字节数组来存储文件数据
                byte[] data = new byte[fs.Length];

                // 从文件流中读取数据并存储到字节数组中
                int bytesRead = fs.Read(data, 0, data.Length);

                Person deserializedPerson = Person.Parser.ParseFrom(data);

            }
        }
    }

    
}
