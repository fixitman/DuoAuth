using System.Reflection;
using Duo;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Settings.Configuration;
using Newtonsoft.Json;
using Microsoft.Data.Sqlite;

internal class Program
{
    private static bool silentMode = false;
    private static string factor = "push";
    private static string ip = "";
    private static string logline = "{user} {ip} {method} {result}";
    private static string logerr = "{user} {ip} {error}";

    private static int Main(string[] args)
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false)
            .AddCommandLine(args)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        if (!String.IsNullOrEmpty(config["silent"]) && config["silent"]!.ToUpper() == "TRUE")
        {
            silentMode = true;
        }

        if (!String.IsNullOrEmpty(config["ip"]))
        {
            ip = config["ip"]!;
        }

        if (String.IsNullOrEmpty(config["username"]))
        {
            Console.WriteLine("Usage: DuoAuth /username <username> [/passcode <passcode>] [/silent true|false]");
            Log.Error(logerr, "", ip, "missing Username");
            return 1;
        }

        if (String.IsNullOrEmpty(config["DUO_KEYS:IKEY"]) ||
            String.IsNullOrEmpty(config["DUO_KEYS:SKEY"]) ||
            String.IsNullOrEmpty(config["DUO_KEYS:HOST"]))
        {
            Console.WriteLine("Invalid configuration. Check appsettings.json");
            Log.Error(logerr, config["username"], ip, "invalid configuration - check appsettings");
            return 2;
        }

        if (!String.IsNullOrEmpty(config["passcode"]))
        {
            factor = "passcode";
        }

        //check for database
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data.db");
            using var connection = new SqliteConnection($"Data Source = {path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"select timestamp from Logins where user = '{config["username"]}' and devid = '{ip}'";

            using var reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                reader.Read();
                var t = reader.GetString(0);
                var stamp = DateTime.Parse(t);
                var span = DateTime.UtcNow - stamp;
                if (span < TimeSpan.FromHours(int.Parse(config["RememberHours"]!)))
                {
                    Out("Remembered device, no auth required");
                    Log.Information(logline, config["username"], ip, factor, "Remembered");
                    return 0;
                }
            }
        }
        catch (SqliteException e)
        {
            Out($"Sql Exception reading - {e.Message}");
            Log.Error(logerr, config["username"], ip, $"SQL Read error - {e.Message}");
            return 5;
        }
        catch (Exception e)
        {
            Out($"Exception - {e.Message}");
            Log.Error(logerr, config["username"], ip, $"Error - {e.Message}");
            return 4;
        }

        DuoApi api = new(config["DUO_KEYS:IKEY"]!, config["DUO_KEYS:SKEY"]!, config["DUO_KEYS:HOST"]!);

        Out($"Sending Authentication Request for {config["username"]}...");

        var parameters = new Dictionary<string, string>(){
            {"username",config["username"]!},
            {"factor",factor}
        };

        if (factor == "passcode")
        {
            parameters.Add("passcode", config["passcode"]!);
        }
        else
        {
            parameters.Add("device", "auto");
        }

        Log.Debug("sending Auth request {user}, {ip}, {method}", config["username"], ip, factor);

        var responseJson = api.ApiCall("POST", "/auth/v2/auth", parameters);

        if (responseJson.Contains("OK"))
        {
            var responseObj = JsonConvert.DeserializeObject<DuoResponse>(responseJson);
            if (responseObj?.response?.result == "deny")
            {
                Out($"Authentication Failed. {responseObj.response.status_msg}");
                Log.Information(logline, config["username"], ip, factor, "Denied");
                return 1;
            }
            else if (responseObj?.response?.result == "allow")
            {
                Out(responseObj.response.status_msg ?? "Success");

                Log.Information(logline, config["username"], ip, factor, "Allowed");

                // todo: update database with successful login

                try
                {
                    var path = Path.Combine(AppContext.BaseDirectory, "data.db");
                    using var connection = new SqliteConnection($"Data Source = {path}");
                    connection.Open();

                    using var command = connection.CreateCommand();
                    command.CommandText = $"insert into logins values ('{config["username"]}','{ip}',datetime()) on conflict do update set timestamp = datetime();";

                    var r = command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Out($"Sql Exception writing - {e.Message}");
                    Log.Error(logerr, config["username"], ip, $"SQL write Error - {e.Message}");
                    return 6;
                }
                
                return 0;
            }
            else
            {
                Out("Unrecognized response/result");
                Log.Error(logerr, config["username"], ip, $"unrecognized response/result - {responseObj?.response?.result}");
                return 2;
            }
        }
        else
        {
            var fail = JsonConvert.DeserializeObject<FailResponse>(responseJson);
            Out($"Authentcation request failed: {fail?.message} - {fail?.message_detail}");
            Log.Error(logerr, config["username"], ip, $"Auth request failed - {fail?.message} - {fail?.message_detail}");
            return 3;
        }
    }

    private static void Out(string s){
        if(!silentMode) Console.WriteLine(s);
    }
}