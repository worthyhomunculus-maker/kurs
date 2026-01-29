using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Указываем порты
builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:5001");

// Простой CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");

// Выход из аккаунта
app.MapPost("/logout", () =>
{
    return Results.Ok(new
    {
        message = "Выход выполнен",
        success = true
    });
});

// Регистрация пользователя
app.MapPost("/register", ([FromBody] AuthRequest request) =>
{
    if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest("Username и password обязательны");

    var data = Storage.LoadData();

    if (data.Users.Any(u => u.Username == request.Username))
        return Results.BadRequest("Пользователь с таким username уже существует");

    var user = new User
    {
        Id = data.NextUserId++,
        Username = request.Username,
        Password = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Password))
    };

    data.Users.Add(user);
    
    data.History.Add(new HistoryItem
    {
        Action = "register",
        UserId = user.Id,
        Username = user.Username,
        Details = "Пользователь зарегистрирован",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = "Пользователь успешно зарегистрирован",
        userId = user.Id,
        username = user.Username
    });
});

// Авторизация
app.MapPost("/login", ([FromBody] AuthRequest request) =>
{
    var data = Storage.LoadData();
    var encodedPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Password));

    var user = data.Users.FirstOrDefault(u =>
        u.Username == request.Username && u.Password == encodedPassword);

    if (user == null)
        return Results.Unauthorized();

    data.History.Add(new HistoryItem
    {
        Action = "login",
        UserId = user.Id,
        Username = user.Username,
        Details = "Успешная авторизация",
        Timestamp = DateTime.Now
    });
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = "Авторизация успешна",
        userId = user.Id,
        username = user.Username
    });
});

// История запросов пользователя
app.MapGet("/users/{userId}/history", (int userId) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);

    if (user == null)
        return Results.NotFound($"Пользователь с ID {userId} не найден");

    var userHistory = data.History
        .Where(h => h.UserId == userId)
        .OrderByDescending(h => h.Timestamp)
        .ToList();

    return Results.Ok(new
    {
        userId = userId,
        username = user.Username,
        history = userHistory
    });
});

// Удалить историю запросов пользователя
app.MapDelete("/users/{userId}/history", (int userId) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);

    if (user == null)
        return Results.NotFound($"Пользователь с ID {userId} не найден");

    var deletedCount = data.History.RemoveAll(h => h.UserId == userId);
    
    data.History.Add(new HistoryItem
    {
        Action = "clear_history",
        UserId = userId,
        Username = user.Username,
        Details = $"Удалено {deletedCount} записей истории",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = $"История пользователя {user.Username} очищена",
        deletedRecords = deletedCount
    });
});

// Изменить пароль
app.MapPatch("/users/{userId}/password", ([FromBody] ChangePasswordRequest request) =>
{
    if (string.IsNullOrEmpty(request.NewPassword))
        return Results.BadRequest("NewPassword обязателен");

    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == request.UserId);

    if (user == null)
        return Results.NotFound($"Пользователь с ID {request.UserId} не найден");

    if (!string.IsNullOrEmpty(request.OldPassword))
    {
        var encodedOldPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.OldPassword));
        if (user.Password != encodedOldPassword)
            return Results.BadRequest("Старый пароль неверен");
    }

    user.Password = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.NewPassword));
    
    data.History.Add(new HistoryItem
    {
        Action = "change_password",
        UserId = user.Id,
        Username = user.Username,
        Details = "Пароль изменен",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = "Пароль успешно изменен",
        userId = user.Id,
        username = user.Username
    });
});

// Удалить пользователя
app.MapDelete("/users/{userId}", (int userId) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);

    if (user == null)
        return Results.NotFound($"Пользователь с ID {userId} не найден");

    data.Users.Remove(user);
    
    data.History.Add(new HistoryItem
    {
        Action = "delete_user",
        Username = user.Username,
        Details = $"Пользователь и все его данные удалены",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = $"Пользователь {user.Username} удален"
    });
});

// Передать массив на сервер
app.MapPost("/users/{userId}/array/set", (int userId, [FromBody] SetArrayRequest request) =>
{
    if (request.Numbers == null)
        return Results.BadRequest("Массив numbers обязателен");

    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    user.CurrentArray = request.Numbers;
    
    data.History.Add(new HistoryItem
    {
        Action = "set_array",
        UserId = userId,
        Username = user.Username,
        Details = $"Установлен массив: [{string.Join(", ", request.Numbers)}]",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);
    
    return Results.Ok(new
    {
        message = "Массив успешно установлен",
        array = request.Numbers,
        length = request.Numbers.Length
    });
});

// Отсортировать массив
app.MapPost("/users/{userId}/array/sort", (int userId) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    var currentArray = user.CurrentArray;
    if (currentArray == null || currentArray.Length == 0)
        return Results.BadRequest("Массив не установлен");

    var originalArray = (int[])currentArray.Clone();
    var sortedArray = GnomeSort.Sort(currentArray);
    user.CurrentArray = sortedArray;

    user.SortedArrays.Add(new ArrayData
    {
        OriginalArray = originalArray,
        SortedArray = sortedArray,
        Timestamp = DateTime.Now
    });
    
    data.History.Add(new HistoryItem
    {
        Action = "sort_array",
        UserId = userId,
        Username = user.Username,
        Details = $"Отсортирован массив. Исходный: [{string.Join(", ", originalArray)}], Результат: [{string.Join(", ", sortedArray)}]",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = "Массив отсортирован",
        originalArray = originalArray,
        sortedArray = sortedArray
    });
});

// Получить текущий массив пользователя
app.MapGet("/users/{userId}/array", (int userId) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    var currentArray = user.CurrentArray;
    if (currentArray == null || currentArray.Length == 0)
        return Results.NotFound("Массив не установлен");

    return Results.Ok(new
    {
        array = currentArray,
        length = currentArray.Length,
    });
});

// Получить часть массива
app.MapGet("/users/{userId}/array/range/{fromIndex}/{toIndex}", (int userId, int fromIndex, int toIndex) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    var currentArray = user.CurrentArray;
    if (currentArray == null || currentArray.Length == 0)
        return Results.NotFound("Массив не установлен");

    if (fromIndex < 0 || fromIndex >= currentArray.Length)
        return Results.BadRequest($"Индекс fromIndex ({fromIndex}) вне диапазона массива");

    if (toIndex < 0 || toIndex >= currentArray.Length)
        return Results.BadRequest($"Индекс toIndex ({toIndex}) вне диапазона массива");

    if (fromIndex > toIndex)
        return Results.BadRequest("fromIndex не может быть больше toIndex");

    var length = toIndex - fromIndex + 1;
    var subArray = new int[length];
    Array.Copy(currentArray, fromIndex, subArray, 0, length);

    return Results.Ok(new
    {
        fromIndex = fromIndex,
        toIndex = toIndex,
        subArray = subArray,
        length = length
    });
});

// Добавить элементы в массив
app.MapPatch("/users/{userId}/array/add", (int userId, [FromBody] AddToArrayRequest request) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    var currentArray = user.CurrentArray;
    if (currentArray == null)
        return Results.BadRequest("Массив не установлен");

    if (request.Elements == null || request.Elements.Length == 0)
        return Results.BadRequest("Элементы для добавления обязательны");

    int[] newArray;
    switch (request.Position?.ToLower())
    {
        case "start":
            newArray = request.Elements.Concat(currentArray).ToArray();
            break;
        case "end":
            newArray = currentArray.Concat(request.Elements).ToArray();
            break;
        case "after":
            if (request.Index == null)
                return Results.BadRequest("Индекс обязателен для позиции 'after'");
            if (request.Index < 0 || request.Index >= currentArray.Length)
                return Results.BadRequest($"Индекс ({request.Index}) вне диапазона массива");
            var tempList = currentArray.ToList();
            tempList.InsertRange(request.Index.Value + 1, request.Elements);
            newArray = tempList.ToArray();
            break;
        default:
            return Results.BadRequest("Недопустимая позиция. Допустимые значения: 'start', 'end', 'after'");
    }

    user.CurrentArray = newArray;
    
    data.History.Add(new HistoryItem
    {
        Action = "modify_array",
        UserId = userId,
        Username = user.Username,
        Details = $"Добавлены элементы в {request.Position}. Новый массив: [{string.Join(", ", newArray)}]",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = $"Элементы добавлены в {request.Position}",
        newArray = newArray,
        newLength = newArray.Length
    });
});

// Сгенерировать случайный массив
app.MapPost("/users/{userId}/array/generate", (int userId, [FromBody] GenerateArrayRequest request) =>
{
    if (request.Size <= 0)
        return Results.BadRequest("Размер массива должен быть положительным числом");

    if (request.MinValue > request.MaxValue)
        return Results.BadRequest("Минимальное значение не может быть больше максимального");

    var random = new Random();
    var newArray = new int[request.Size];
    for (int i = 0; i < request.Size; i++)
    {
        newArray[i] = random.Next(request.MinValue, request.MaxValue + 1);
    }

    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    user.CurrentArray = newArray;
    
    data.History.Add(new HistoryItem
    {
        Action = "generate_array",
        UserId = userId,
        Username = user.Username,
        Details = $"Сгенерирован массив размером {request.Size}. Массив: [{string.Join(", ", newArray)}]",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = "Случайный массив сгенерирован",
        array = newArray,
        length = newArray.Length,
        minValue = request.MinValue,
        maxValue = request.MaxValue
    });
});

// Удалить массив
app.MapDelete("/users/{userId}/array", (int userId) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    var currentArray = user.CurrentArray;
    if (currentArray == null || currentArray.Length == 0)
        return Results.NotFound("Массив уже пустой");

    var deletedArray = currentArray;
    user.CurrentArray = Array.Empty<int>();

    data.History.Add(new HistoryItem
    {
        Action = "delete_array",
        UserId = userId,
        Username = user.Username,
        Details = $"Удален массив: [{string.Join(", ", deletedArray)}]",
        Timestamp = DateTime.Now
    });
    
    Storage.SaveData(data);

    return Results.Ok(new
    {
        message = "Массив удален",
        deletedArray = deletedArray
    });
});

// История всех операций
app.MapGet("/history", () =>
{
    var data = Storage.LoadData();
    return Results.Ok(new
    {
        totalRecords = data.History.Count,
        history = data.History.OrderByDescending(h => h.Timestamp).ToList()
    });
});

// ОЧИСТКА ВСЕЙ ИСТОРИИ (новое)
app.MapDelete("/history", () =>
{
    var data = Storage.LoadData();
    data.History.Clear();
    Storage.SaveData(data);
    return Results.Ok(new { message = "Вся история очищена" });
});

// Получить все отсортированные массивы пользователя
app.MapGet("/users/{userId}/sorted-arrays", (int userId) =>
{
    var data = Storage.LoadData();
    var user = data.Users.FirstOrDefault(u => u.Id == userId);
    if (user == null)
        return Results.NotFound("Пользователь не найден");

    return Results.Ok(new
    {
        count = user.SortedArrays.Count,
        arrays = user.SortedArrays.OrderByDescending(a => a.Timestamp).ToList()
    });
});

// === НОВЫЙ ЭНДПОИНТ: СТАТИСТИКА ===
app.MapGet("/statistics", () =>
{
    var data = Storage.LoadData();
    
    // Общее количество сортировок
    var sortCount = data.History.Count(h => 
        h.Action == "sort_array" || 
        h.Action == "sort_array_with_simple_steps" ||
        h.Action == "sort_simple_steps" ||
        h.Action == "sort"
    );
    
    // Количество пользователей
    var userCount = data.Users.Count;
    
    // Количество активных массивов
    var activeArrays = data.Users.Count(u => u.CurrentArray?.Length > 0);
    
    // Общее количество отсортированных массивов
    var totalSortedArrays = data.Users.Sum(u => u.SortedArrays.Count);
    
    // Последняя операция сортировки
    var lastSort = data.History
        .Where(h => h.Action.StartsWith("sort"))
        .OrderByDescending(h => h.Timestamp)
        .FirstOrDefault();

    return Results.Ok(new
    {
        users = userCount,
        activeArrays,
        totalSortedArrays,
        totalHistoryRecords = data.History.Count,
        sortOperations = sortCount,
        lastSortAction = lastSort?.Action ?? "—",
        lastSortTime = lastSort?.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") ?? "—",
        lastSortUser = lastSort?.Username ?? "—"
    });
});

// Главная страница
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();

// ========== КЛАССЫ ==========

public record AuthRequest(string Username, string Password);
public record SortRequest(int[] Numbers);
public record SetArrayRequest(int[] Numbers);
public record AddToArrayRequest(string Position, int[] Elements, int? Index = null);
public record GenerateArrayRequest(int Size, int MinValue = 0, int MaxValue = 100);
public record ChangePasswordRequest(int UserId, string? OldPassword, string NewPassword);

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int[] CurrentArray { get; set; } = Array.Empty<int>();
    public List<ArrayData> SortedArrays { get; set; } = new();
}

public class HistoryItem
{
    public string Action { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ArrayData
{
    public int[] OriginalArray { get; set; } = Array.Empty<int>();
    public int[] SortedArray { get; set; } = Array.Empty<int>();
    public DateTime Timestamp { get; set; }
}

public class Data
{
    public List<User> Users { get; set; } = new();
    public List<HistoryItem> History { get; set; } = new();
    public int NextUserId { get; set; } = 1;
}

public static class Storage
{
    private static readonly string DataFile = "data.json";
    private static readonly object Lock = new();

    public static Data LoadData()
    {
        lock (Lock)
        {
            if (!File.Exists(DataFile))
                return new Data();

            try
            {
                var json = File.ReadAllText(DataFile);
                var data = JsonSerializer.Deserialize<Data>(json) ?? new Data();

                if (data.Users.Count > 0)
                    data.NextUserId = data.Users.Max(u => u.Id) + 1;

                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading data: {ex.Message}");
                return new Data();
            }
        }
    }

    public static void SaveData(Data data)
    {
        lock (Lock)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(data, options);
                
                json = FormatCurrentArrayInOneLine(json);
                
                File.WriteAllText(DataFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving data: {ex.Message}");
            }
        }
    }

    private static string FormatCurrentArrayInOneLine(string json)
    {
        var pattern = @"(""CurrentArray""\s*:\s*)\[[^\]]*\]";
        var match = Regex.Match(json, pattern);
        
        if (match.Success)
        {
            var arrayMatch = Regex.Match(match.Value, @"\[[^\]]*\]");
            if (arrayMatch.Success)
            {
                var cleanArray = arrayMatch.Value
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Replace("  ", " ")
                    .Replace(" , ", ",")
                    .Replace(", ", ",");
                
                return json.Replace(arrayMatch.Value, cleanArray);
            }
        }
        
        return json;
    }
}

// ========== ГНОМЬЯ СОРТИРОВКА ==========

public static class GnomeSort
{
    public static int[] Sort(int[] array)
    {
        if (array == null || array.Length <= 1)
            return array ?? Array.Empty<int>();

        int[] sortedArray = (int[])array.Clone();
        int index = 0;

        while (index < sortedArray.Length)
        {
            if (index == 0)
            {
                index++;
            }
            else if (sortedArray[index] >= sortedArray[index - 1])
            {
                index++;
            }
            else
            {
                (sortedArray[index], sortedArray[index - 1]) = (sortedArray[index - 1], sortedArray[index]);
                index--;
            }
        }

        return sortedArray;
    }
}