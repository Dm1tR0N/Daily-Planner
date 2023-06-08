using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Windows.Storage.FileProperties;
using System.Threading.Tasks;
using Windows.Data.Html;
using static Daily_Planner.MainPage;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x419

namespace Daily_Planner
{
    public sealed partial class MainPage : Page
    {
        public class Task
        {
            public string TaskName { get; set; }
            public DateTime DateTime { get; set; }
            public string Description { get; set; }
        }

        public MainPage()
        {
            this.InitializeComponent();

            LoadTasks();
            HighlightClosestTask();        
        }

        private async void LoadTasks()
        {
            StorageFolder documentsFolder1 = KnownFolders.DocumentsLibrary;
            StorageFolder localFolder = await documentsFolder1.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);


            try
            {
                StorageFile tasksFile = await localFolder.GetFileAsync("tasks.xml");
                using (Stream stream = await tasksFile.OpenStreamForReadAsync())
                {
                    XDocument xmlDoc = XDocument.Load(stream);

                    var tasks = xmlDoc.Descendants("Task")
                        .Select(task => new Task
                        {
                            TaskName = task.Attribute("Name").Value,
                            DateTime = DateTime.Parse(task.Attribute("Date").Value),
                            Description = task.Value.Trim()
                        })
                        .Where(task => (task.DateTime - DateTime.Now).Days <= 30)
                        .ToList();

                    taskListView.ItemsSource = tasks;
                }
            }
            catch (FileNotFoundException ex)
            {
                textBox.Text = $"Ошибка при загрузке задач: {ex.Message}";
            }
        }
        private void HighlightClosestTask()
        {
            // Получение текущей даты
            DateTime today = DateTime.Today;

            // Поиск задачи с самым близким числом к текущей дате
            Task closestTask = taskListView.Items
                .OfType<Task>()
                .OrderBy(task => Math.Abs((task.DateTime.Date - today).Days))
                .FirstOrDefault();

            // Подписка на событие Loaded элементов списка
            taskListView.Loaded += (sender, e) =>
            {
                // Обновление визуального представления задачи
                if (closestTask != null)
                {
                    // Находим соответствующий элемент списка
                    var itemContainer = taskListView.ContainerFromItem(closestTask) as ListViewItem;

                    // Устанавливаем жёлтый цвет для фона элемента
                    if (itemContainer != null)
                    {
                        itemContainer.Background = new SolidColorBrush(Colors.Yellow);
                    }
                }
            };
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string textToSave = textBox.Text;
            DateTime currentDate = DateTime.Now;

            StorageFolder documentsFolder1 = KnownFolders.DocumentsLibrary;
            StorageFolder localFolder = await documentsFolder1.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);

            // Создание или открытие файла
            StorageFile saveFile = await localFolder.CreateFileAsync("save.xml", CreationCollisionOption.OpenIfExists);

            XDocument xmlDoc;

            // Проверяем, существует ли файл или он только что создан
            if (saveFile != null)
            {
                // Получаем свойства файла
                BasicProperties fileProperties = await saveFile.GetBasicPropertiesAsync();

                // Если размер файла больше 0, загружаем его содержимое
                if (fileProperties.Size > 0)
                {
                    using (Stream stream = await saveFile.OpenStreamForReadAsync())
                    {
                        xmlDoc = XDocument.Load(stream);
                    }
                    LoadTasks();
                    HighlightClosestTask();
                }
                else
                {
                    // Если файл только что создан, создаем новый XML-документ с корневым элементом
                    xmlDoc = new XDocument(new XElement("Entries"));
                }

                // Находим существующий элемент Entry с датой, соответствующей текущей дате
                XElement existingEntry = xmlDoc.Root.Elements("Entry")
                    .FirstOrDefault(entry => entry.Attribute("Date").Value == currentDate.ToString());

                if (existingEntry != null)
                {
                    // Если существующий элемент найден, обновляем его значение
                    existingEntry.Value = textToSave;
                }
                else
                {
                    // Если существующий элемент не найден, создаем новый элемент Entry
                    XElement newEntry = new XElement("Entry");
                    newEntry.SetAttributeValue("Date", currentDate.ToString());
                    newEntry.Value = textToSave;

                    // Добавляем новый элемент Entry в корневой элемент XML-документа
                    xmlDoc.Root.Add(newEntry);
                }

                // Сохранение XML-документа обратно в файл
                using (Stream stream = await saveFile.OpenStreamForWriteAsync())
                {
                    xmlDoc.Save(stream);
                }
            }
        }

        private async void CalendarView_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            try
            {
                DateTime selectedDate = args.AddedDates.FirstOrDefault().DateTime.Date;

                StorageFolder documentsFolder1 = KnownFolders.DocumentsLibrary;
                StorageFolder localFolder = await documentsFolder1.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);

                XDocument xmlDoc;
                using (Stream stream = await localFolder.OpenStreamForReadAsync("save.xml"))
                {
                    xmlDoc = XDocument.Load(stream);
                }

                var entries = xmlDoc.Descendants("Entry")
                    .Where(entry => DateTime.Parse(entry.Attribute("Date").Value).Date == selectedDate)
                    .Select(entry => entry.Value);

                textBox.Text = string.Join(Environment.NewLine, entries);
                EditTaskMenu.Visibility = Visibility.Collapsed;
                if (textBox.Text == "") { textBox.Text = "Результатов об этом дне нет."; };
            }
            catch (Exception ex)
            {
                textBox.Text = $"Ошибка при загрузке записей для выбранной даты: {ex.Message}";
            }
        }

        private async void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string taskName = textBox.GetFirstLine(); // Получаем первую строку из textBox
            string taskDescription = textBox.RemoveFirstLine(); // Удаляем первую строку из textBox

            DateTime selectedDate = calendarView.SelectedDates.FirstOrDefault().DateTime.Date;

            // Создание или открытие файла
            StorageFolder documentsFolder1 = KnownFolders.DocumentsLibrary;
            StorageFolder localFolder = await documentsFolder1.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);
            StorageFile saveFile = await localFolder.CreateFileAsync("tasks.xml", CreationCollisionOption.OpenIfExists);

            XDocument xmlDoc;

            // Проверяем, существует ли файл или он только что создан
            if (saveFile != null)
            {
                // Получаем свойства файла
                BasicProperties fileProperties = await saveFile.GetBasicPropertiesAsync();

                // Если размер файла больше 0, загружаем его содержимое
                if (fileProperties.Size > 0)
                {
                    using (Stream stream = await saveFile.OpenStreamForReadAsync())
                    {
                        xmlDoc = XDocument.Load(stream);
                    }
                }
                else
                {
                    // Если файл только что создан, создаем новый XML-документ с корневым элементом
                    xmlDoc = new XDocument(new XElement("Tasks"));
                }

                // Создание нового элемента Task
                XElement taskElement = new XElement("Task");
                taskElement.SetAttributeValue("Name", taskName);
                taskElement.SetAttributeValue("Date", selectedDate.ToString("dd.MM.yyyy HH:mm:ss"));
                taskElement.Value = taskDescription;

                // Добавление нового элемента Task в корневой элемент XML-документа
                xmlDoc.Root.Add(taskElement);

                // Сохранение XML-документа обратно в файл
                using (Stream stream = await saveFile.OpenStreamForWriteAsync())
                {
                    xmlDoc.Save(stream);
                }
                UpdateTaskList();
                textBox.Text = "";
            }    
        }

        private async void UpdateTaskList()
        {
            // Получение папки LocalFolder для чтения файла
            StorageFolder documentsFolder1 = KnownFolders.DocumentsLibrary;
            StorageFolder localFolder = await documentsFolder1.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);

            try
            {
                // Открытие файла
                StorageFile tasksFile = await localFolder.GetFileAsync("tasks.xml");
                using (Stream stream = await tasksFile.OpenStreamForReadAsync())
                {
                    // Загрузка XML-документа из потока
                    XDocument xmlDoc = XDocument.Load(stream);

                    // Извлечение задач из XML и преобразование их в объекты Task
                    var tasks = xmlDoc.Descendants("Task")
                        .Select(task => new Task
                        {
                            TaskName = task.Attribute("Name").Value,
                            DateTime = DateTime.Parse(task.Attribute("Date").Value),
                            Description = task.Value.Trim()
                        })
                        .ToList();

                    // Установка списка задач в качестве источника данных для ListView
                    taskListView.ItemsSource = tasks;
                }
            }
            catch (FileNotFoundException ex)
            {
                textBox.Text = $"Ошибка при загрузке задач: {ex.Message}";
            }
        }

        private void TaskListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedTask = (Task)taskListView.SelectedItem;
                if (selectedTask != null)
                {
                    textBox.Text = selectedTask.TaskName;
                    EditTaskMenu.Visibility = Visibility.Visible;
                }
                else
                {
                    textBox.Text = "На этот день нет задач.";
                }
            }
            catch (Exception ex)
            {
                textBox.Text = "Ошибка! " + ex;
            }
        }

        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (taskListView.SelectedItem != null)
            {
                Task selectedTask = (Task)taskListView.SelectedItem;
                selectedTask.TaskName = textBox.Text;

                StorageFolder documentsFolder1 = KnownFolders.DocumentsLibrary;
                StorageFolder localFolder = await documentsFolder1.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);
                StorageFile saveFile = await localFolder.CreateFileAsync("tasks.xml", CreationCollisionOption.OpenIfExists);

                XDocument xmlDoc;
                using (Stream stream = await saveFile.OpenStreamForReadAsync())
                {
                    xmlDoc = XDocument.Load(stream);
                }

                XElement taskElement = xmlDoc.Descendants("Task")
                                             .FirstOrDefault(el => (string)el.Attribute("Date") == selectedTask.DateTime.ToString("dd.MM.yyyy HH:mm:ss"));
                if (taskElement != null)
                {
                    taskElement.SetAttributeValue("Name", selectedTask.TaskName);

                    using (Stream stream = await saveFile.OpenStreamForWriteAsync())
                    {
                        xmlDoc.Save(stream);
                    }
                    LoadTasks();
                    HighlightClosestTask();
                }
               
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (taskListView.SelectedItem != null)
            {
                Task selectedTask = (Task)taskListView.SelectedItem;

                StorageFolder documentsFolder1 = KnownFolders.DocumentsLibrary;
                StorageFolder localFolder = await documentsFolder1.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);
                StorageFile saveFile = await localFolder.CreateFileAsync("tasks.xml", CreationCollisionOption.OpenIfExists);

                XDocument xmlDoc;
                using (Stream stream = await saveFile.OpenStreamForReadAsync())
                {
                    xmlDoc = XDocument.Load(stream);
                }

                XElement taskElement = xmlDoc.Descendants("Task")
                                             .FirstOrDefault(el => el.Attribute("Date").Value.StartsWith(selectedTask.DateTime.ToString("dd.MM.yyyy")));
                if (taskElement != null)
                {
                    taskElement.Remove();

                    using (Stream stream = await saveFile.OpenStreamForWriteAsync())
                    {
                        stream.SetLength(0); // Очищаем содержимое файла перед сохранением
                        xmlDoc.Save(stream);
                    }
                    selectedTask = null;
                    LoadTasks();
                    HighlightClosestTask();
                }
            }
        }

        private async void UpdateDateButton_Click(object sender, RoutedEventArgs e)
        {
            if (taskListView.SelectedItem != null && datePicker.SelectedDate.HasValue)
            {
                Task selectedTask = (Task)taskListView.SelectedItem;

                DateTimeOffset selectedDate = datePicker.SelectedDate.Value.Date;
                DateTime selectedDateTime = selectedDate.DateTime + selectedTask.DateTime.TimeOfDay;

                StorageFolder documentsFolder = KnownFolders.DocumentsLibrary;
                StorageFolder dailyPlannerFolder = await documentsFolder.CreateFolderAsync("Daily Planner", CreationCollisionOption.OpenIfExists);
                StorageFile saveFile = await dailyPlannerFolder.CreateFileAsync("tasks.xml", CreationCollisionOption.OpenIfExists);

                XDocument xmlDoc;
                using (Stream stream = await saveFile.OpenStreamForReadAsync())
                {
                    xmlDoc = XDocument.Load(stream);
                }

                XElement taskElement = xmlDoc.Descendants("Task")
                    .FirstOrDefault(el => el.Attribute("Date")?.Value == selectedTask.DateTime.ToString("dd.MM.yyyy HH:mm:ss"));

                if (taskElement != null)
                {
                    taskElement.Attribute("Date").Value = selectedDateTime.ToString("dd.MM.yyyy HH:mm:ss");

                    using (Stream stream = await saveFile.OpenStreamForWriteAsync())
                    {
                        xmlDoc.Save(stream);
                    }

                    LoadTasks();
                    HighlightClosestTask();
                    textBox.Text = "";
                }
            }
        }

    }
    public static class TextBoxExtensions
    {
        public static string GetFirstLine(this TextBox textBox)
        {
            int firstLineEndIndex = textBox.Text.IndexOf(Environment.NewLine);
            if (firstLineEndIndex != -1)
            {
                return textBox.Text.Substring(0, firstLineEndIndex);
            }
            else
            {
                return textBox.Text;
            }
        }

        public static string RemoveFirstLine(this TextBox textBox)
        {
            int firstLineEndIndex = textBox.Text.IndexOf(Environment.NewLine);
            if (firstLineEndIndex != -1)
            {
                return textBox.Text.Substring(firstLineEndIndex + Environment.NewLine.Length);
            }
            else
            {
                return string.Empty;
            }
        }
    }

}
