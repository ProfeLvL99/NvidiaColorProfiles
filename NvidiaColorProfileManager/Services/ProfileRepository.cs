using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Repositorio de perfiles usando SQLite con export/import JSON.
/// </summary>
public class ProfileRepository : IProfileRepository
{
    private readonly string _connectionString;

    public ProfileRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Profiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                SettingsJson TEXT NOT NULL,
                DisplayId INTEGER NOT NULL DEFAULT 0,
                DisplayName TEXT NOT NULL DEFAULT 'Todos los monitores',
                Hotkey TEXT,
                CreatedAt TEXT NOT NULL,
                ModifiedAt TEXT NOT NULL
            )";
        command.ExecuteNonQuery();

        // Migración: agregar columna Hotkey si no existe
        try
        {
            command.CommandText = "ALTER TABLE Profiles ADD COLUMN Hotkey TEXT";
            command.ExecuteNonQuery();
        }
        catch { /* La columna ya existe */ }
    }

    public List<ColorProfile> GetAll()
    {
        var profiles = new List<ColorProfile>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, SettingsJson, DisplayId, DisplayName, Hotkey, CreatedAt, ModifiedAt FROM Profiles ORDER BY ModifiedAt DESC";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var settings = JsonSerializer.Deserialize<ColorSettings>(reader.GetString(2)) ?? new ColorSettings();

            profiles.Add(new ColorProfile
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Settings = settings,
                DisplayId = reader.GetInt32(3),
                DisplayName = reader.GetString(4),
                Hotkey = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                ModifiedAt = DateTime.Parse(reader.GetString(7))
            });
        }

        return profiles;
    }

    public ColorProfile? GetById(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, SettingsJson, DisplayId, DisplayName, Hotkey, CreatedAt, ModifiedAt FROM Profiles WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var settings = JsonSerializer.Deserialize<ColorSettings>(reader.GetString(2)) ?? new ColorSettings();

            return new ColorProfile
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Settings = settings,
                DisplayId = reader.GetInt32(3),
                DisplayName = reader.GetString(4),
                Hotkey = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                ModifiedAt = DateTime.Parse(reader.GetString(7))
            };
        }

        return null;
    }

    public void Add(ColorProfile profile)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Profiles (Name, SettingsJson, DisplayId, DisplayName, Hotkey, CreatedAt, ModifiedAt)
            VALUES (@Name, @SettingsJson, @DisplayId, @DisplayName, @Hotkey, @CreatedAt, @ModifiedAt)";

        profile.CreatedAt = DateTime.Now;
        profile.ModifiedAt = DateTime.Now;
        var settingsJson = JsonSerializer.Serialize(profile.Settings);

        command.Parameters.AddWithValue("@Name", profile.Name);
        command.Parameters.AddWithValue("@SettingsJson", settingsJson);
        command.Parameters.AddWithValue("@DisplayId", profile.DisplayId);
        command.Parameters.AddWithValue("@DisplayName", profile.DisplayName);
        command.Parameters.AddWithValue("@Hotkey", (object?)profile.Hotkey ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", profile.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@ModifiedAt", profile.ModifiedAt.ToString("o"));

        command.ExecuteNonQuery();

        // Obtener el ID autogenerado
        command.CommandText = "SELECT last_insert_rowid()";
        profile.Id = (int)(long)command.ExecuteScalar()!;
    }

    public void Update(ColorProfile profile)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Profiles
            SET Name = @Name, SettingsJson = @SettingsJson, DisplayId = @DisplayId,
                DisplayName = @DisplayName, Hotkey = @Hotkey, ModifiedAt = @ModifiedAt
            WHERE Id = @Id";

        profile.ModifiedAt = DateTime.Now;
        var settingsJson = JsonSerializer.Serialize(profile.Settings);

        command.Parameters.AddWithValue("@Id", profile.Id);
        command.Parameters.AddWithValue("@Name", profile.Name);
        command.Parameters.AddWithValue("@SettingsJson", settingsJson);
        command.Parameters.AddWithValue("@DisplayId", profile.DisplayId);
        command.Parameters.AddWithValue("@DisplayName", profile.DisplayName);
        command.Parameters.AddWithValue("@Hotkey", (object?)profile.Hotkey ?? DBNull.Value);
        command.Parameters.AddWithValue("@ModifiedAt", profile.ModifiedAt.ToString("o"));

        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Profiles WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        command.ExecuteNonQuery();
    }

    public List<ColorProfile> ImportFromJson(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var profiles = JsonSerializer.Deserialize<List<ColorProfile>>(json) ?? new List<ColorProfile>();

        foreach (var profile in profiles)
        {
            profile.Id = 0; // Reset ID para que SQLite asigne uno nuevo
            Add(profile);
        }

        return profiles;
    }

    public void ExportToJson(ColorProfile profile, string filePath)
    {
        var profiles = new List<ColorProfile> { profile };
        var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }
}
