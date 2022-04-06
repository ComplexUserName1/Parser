using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AngleSharp;
using AngleSharp.Dom;
using System.Threading;
using System.IO;

namespace Parsing
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        public object locker; //локер для приоритета потока, который в данный момент записывает данные в файл
        public string MainUrl; //ссылка на сайт, который мы вводим в textBox1
        public string GlobalPath; //путь к папке, где нужно создать CSV файл
        public int pages; //отдельная переменная, которая нужна для проверки завершения работы потоков
        private async void button1_Click(object sender, EventArgs e)
        {
            textBox1.Enabled = false;
            textBox2.Enabled = false;
            button1.Enabled = false;
            MainUrl = textBox1.Text;
            locker = new object();
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var doc = await context.OpenAsync(MainUrl); //подгружаем сайт, с которого нам нужны данные(без JS скриптов)
            try
            {
                Thread[] EachPageThreads = new Thread[Convert.ToInt32(doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item")[doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item").Length - 2].QuerySelector("a").TextContent.Trim())]; //создаём массив потоков, для работы с данными(количество потоков зависит от количества страниц)
                pages = Convert.ToInt32(doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item")[doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item").Length - 2].QuerySelector("a").TextContent.Trim());
                for (int page = 0; page < Convert.ToInt32(doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item")[doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item").Length - 2].QuerySelector("a").TextContent.Trim()); page++)
                {
                    string url = "https://www.toy.ru" + doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item")[2].QuerySelector("a").GetAttribute("href").Remove(doc.GetElementsByClassName("pagination justify-content-between")[0].GetElementsByClassName("page-item")[2].QuerySelector("a").GetAttribute("href").Length - 1, 1) + Convert.ToString(page + 1); //собираем ссылку на каждую страницу с нужными нам товарами
                    EachPageThreads[page] = new Thread(EachPageParse);
                    EachPageThreads[page].Start(url); //запускаем парсинг данных для каждой страницы с товарами
                }
            }
            catch(ArgumentOutOfRangeException)
            {
                pages = 1;
                EachPageParse(MainUrl); //если на странице с товарами страниц не больше 1
            }
        }
        public struct Toys //ну тут в принципе всё понятно.(подготавливаем скелет структуры, которую потом будем заполнять данными)
        {
            public string region;
            public string breadcrumbs;
            public string detail_name;
            public string price;
            public string old_price;
            public string ok;
            public string images_hrefs;
            public string toy_href;
        }
        public async void EachPageParse(object obj)
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var doc = await context.OpenAsync(Convert.ToString(obj));
            lock (locker) //блокируем проход другим потокам, кроме главного. как только монополизирующий поток закончит свои действия, другой поток сможет приступить к выполнению этих действий.
            {
                var docs = new List<Task<IDocument>>();
                Toys[] toys1 = new Toys[Convert.ToInt32(doc.GetElementsByClassName("show-by")[0].GetElementsByClassName("item active")[0].QuerySelector("a").TextContent.Trim())]; //создаём массив структур.(всего структур = количеству товаров)
                var ToysUrl = doc.GetElementsByClassName("row mt-2")[0].GetElementsByClassName("col-12 col-sm-6 col-md-6 col-lg-4 col-xl-4 my-2"); //вытаскиваем ссылки на все товары
                for (int i = 0; i <= ToysUrl.Length - 1; i++)
                {
                    toys1[i].toy_href = ToysUrl[i].QuerySelector("meta").GetAttribute("content"); //заполняем ссылку на товар в элемент структуры
                    docs.Add(context.OpenAsync(ToysUrl[i].QuerySelector("meta").GetAttribute("content"))); //подготавливаем массив документов, чтобы потом подгрузить их вытаскивать нужные нам данные из каждого товара
                }
                Task.WaitAll(docs.ToArray()); //подгружаем страницу с каждым товаром
                StructBuilder(toys1, docs); //заполняем структуру с данными каждого товара данной страницы
                WriteToCSVFile(toys1, ToysUrl); //записываем всё в файл
                listBox1.Invoke(new Action(() => listBox1.Items.Add("Поток закончил свою работу"))); //добавляем в листбокс элемент для проверки окончания работы всех потоков
                if (listBox1.Items.Count == pages)
                {
                    MessageBox.Show("Парсинг завершён", "Внимание"); //выводим сообщение об окончании работы программы
                    Application.Exit(); //выходим из программы
                }
            }
        }
        public void WriteToCSVFile(Toys[] toys1, IHtmlCollection<IElement> ToysUrl)
        {
            StringBuilder ResultStringForCSV = new StringBuilder(); //подготавливаем стрингбилдер. именно с помощью него мы собираем строку с нужными нам данными в нужном для записи в файл формате.
            for (int j = 0; j <= ToysUrl.Length - 1; j++)
            {
                ResultStringForCSV.AppendLine(toys1[j].region + ";" + toys1[j].breadcrumbs + ";" + toys1[j].detail_name + ";" + toys1[j].price + ";" + toys1[j].old_price + ";" + toys1[j].ok + ";" + toys1[j].images_hrefs + toys1[j].toy_href); //собираем строку с данными товара
                GlobalPath = textBox2.Text.Replace(@"\",@"\\") + @"\\Parsing_Data.csv"; //вытаскиваем и модернизируем путь к папке, где нужно будет создать файл.
                File.AppendAllText(GlobalPath, ResultStringForCSV.ToString(), Encoding.GetEncoding(1251)); //записываем данные в файл.(если файла изначально нет, то он его создаст)
                ResultStringForCSV.Clear(); //отчищаем стрингбилдер, чтобы данные не дублировались.
            }
        }
        public void StructBuilder(Toys[] toys1, List<Task<IDocument>> docs)
        {
            var j = 0;
            foreach (var t in docs)
            {
                var res = t.Result;
                toys1[j].region = res.GetElementsByClassName("d-none d-md-block top-line")[0].GetElementsByClassName("col-12 select-city-link")[0].QuerySelector("a").TextContent.Trim(); //заполняем данные о выбранном регионе
                toys1[j].breadcrumbs = res.GetElementsByClassName("breadcrumb")[0].TextContent.Trim(); //заполняем данные о хлебных крошках
                toys1[j].detail_name = res.GetElementsByClassName("detail-name")[0].TextContent.Trim(); //заполняем данные об полном имени товара
                toys1[j].price = res.GetElementsByClassName("price")[0].TextContent.Trim(); //заполняем данные о цене товара
                try
                {
                    toys1[j].old_price = res.GetElementsByClassName("old-price")[0].TextContent.Trim(); //заполняем данные о старой цене товара
                }
                catch (ArgumentOutOfRangeException)
                {
                    toys1[j].old_price = "Нет старой цены";
                }
                try
                {
                    toys1[j].ok = res.GetElementsByClassName("ok")[0].TextContent.Trim(); //заполняем данные о наличии товара(не совсем понял зачем это надо, так как у них на сайте если товара нет в наличии, то он просто не отображается у пользователя)
                }
                catch (ArgumentOutOfRangeException)
                {
                    toys1[j].ok = "Товара нет в наличии";
                }
                for (int f = 0; f <= res.GetElementsByClassName("card-slider-nav")[0].QuerySelectorAll("img").Length - 1; f++)
                {
                    toys1[j].images_hrefs += res.GetElementsByClassName("card-slider-nav")[0].QuerySelectorAll("img")[f].GetAttribute("src") + ";"; //собираем строку с ссылками на картинки товара
                }
                j = j + 1;
                res.Dispose(); //освобождаем ресурсы
            }
        }
    }
}
