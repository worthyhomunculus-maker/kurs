using System.Text;
using Xunit;
using WebApplication1; // ← важно: подключаем пространство имён основного проекта
namespace WebApplication1;


public class UserAndSortLogicTests
{
    // Вспомогательный метод для очистки данных перед каждым тестом
    private void ClearTestData()
    {
        var data = Storage.LoadData();
        data.Users.Clear();
        data.History.Clear();
        data.NextUserId = 1;
        Storage.SaveData(data);
    }

    [Fact]
    public void Register_NewUser_ShouldAddUserToStorage()
    {
        // Подготовка
        ClearTestData();
        string username = "newuser";
        string password = "pass123";

        // Действие: имитация регистрации
        var data = Storage.LoadData();
        var existingUser = data.Users.FirstOrDefault(u => u.Username == username);
        Assert.Null(existingUser);

        var newUser = new User
        {
            Id = data.NextUserId++,
            Username = username,
            Password = Convert.ToBase64String(Encoding.UTF8.GetBytes(password)),
            CurrentArray = Array.Empty<int>() // явно инициализируем
        };
        data.Users.Add(newUser);
        Storage.SaveData(data);

        // Проверка
        var savedData = Storage.LoadData();
        var user = savedData.Users.FirstOrDefault(u => u.Username == username);
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        Assert.Equal(1, user.Id);
    }

    [Fact]
    public void Login_WithCorrectCredentials_ShouldFindUser()
    {
        // Подготовка
        ClearTestData();
        string username = "loginuser";
        string password = "secret";

        // Сначала зарегистрируем пользователя
        var regData = Storage.LoadData();
        regData.Users.Add(new User
        {
            Id = regData.NextUserId++,
            Username = username,
            Password = Convert.ToBase64String(Encoding.UTF8.GetBytes(password)),
            CurrentArray = Array.Empty<int>()
        });
        Storage.SaveData(regData);

        // Действие: попытка входа
        var loginData = Storage.LoadData();
        var encodedPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
        var user = loginData.Users.FirstOrDefault(u =>
            u.Username == username && u.Password == encodedPassword);

        // Проверка
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
    }

    [Fact]
    public void Login_WithWrongPassword_ShouldNotFindUser()
    {
        // Подготовка
        ClearTestData();
        string username = "baduser";
        string correctPassword = "right";
        string wrongPassword = "wrong";

        // Регистрация
        var regData = Storage.LoadData();
        regData.Users.Add(new User
        {
            Id = regData.NextUserId++,
            Username = username,
            Password = Convert.ToBase64String(Encoding.UTF8.GetBytes(correctPassword)),
            CurrentArray = Array.Empty<int>()
        });
        Storage.SaveData(regData);

        // Действие: вход с неправильным паролем
        var loginData = Storage.LoadData();
        var encodedWrong = Convert.ToBase64String(Encoding.UTF8.GetBytes(wrongPassword));
        var user = loginData.Users.FirstOrDefault(u =>
            u.Username == username && u.Password == encodedWrong);

        // Проверка
        Assert.Null(user);
    }

    [Fact]
    public void GnomeSort_ShouldSortArrayCorrectly()
    {
        // Подготовка
        var input = new[] { 4, 2, 7, 1, 3 };

        // Действие
        var sorted = GnomeSort.Sort(input);

        // Проверка
        Assert.Equal(new[] { 1, 2, 3, 4, 7 }, sorted);
    }

    [Fact]
    public void SetAndSortArray_EndToEnd_ShouldWork()
    {
        // Подготовка
        ClearTestData();

        // Создаём пользователя
        var data = Storage.LoadData();
        var user = new User
        {
            Id = data.NextUserId++,
            Username = "testuser",
            Password = "dummy",
            CurrentArray = new[] { 5, 1, 9, 3 }
        };
        data.Users.Add(user);
        Storage.SaveData(data);

        // Сортировка массива пользователя
        var sortData = Storage.LoadData();
        var targetUser = sortData.Users.First(u => u.Id == user.Id);
        var sorted = GnomeSort.Sort(targetUser.CurrentArray);
        targetUser.CurrentArray = sorted;
        Storage.SaveData(sortData);

        // Проверка
        var finalData = Storage.LoadData();
        var finalUser = finalData.Users.First(u => u.Id == user.Id);
        Assert.Equal(new[] { 1, 3, 5, 9 }, finalUser.CurrentArray);
    }
}