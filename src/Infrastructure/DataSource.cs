﻿using System.Globalization;
using System.IO.Compression;
using CsvHelper;
using CsvHelper.Configuration;
using Domain;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

internal class DataSource
{
    private readonly IReaderConfiguration _config;
    private readonly string _date;
    private readonly ILogger<DataSource> _logger;
    private readonly string _urlBase;

    public DataSource(ILogger<DataSource> logger)
    {
        _logger = logger;
        var date = DateTime.UtcNow;
        _date =
            $"{date.Year}-" +
            $"{(date.Month >= 10 ? date.Month.ToString() : "0" + date.Month)}" +
            $"-{(date.Day >= 10 ? (date.Day - 1).ToString() : "0" + (date.Day - 1))}";

        _config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = "\t",
            BadDataFound = null,
            MissingFieldFound = null
        };

        _urlBase = "http://download.geonames.org/export/dump/";
    }

    public async Task<IEnumerable<GeoName>> GetFullDbAsync(CancellationToken token = default)
    {
        try
        {
            _logger.LogInformation("GetFullDBAsync(...) was called.");
            var url = _urlBase + "cities500.zip";
            await using var zip = await DownloadFileAsync(url, token);

            var files = Unzip(zip);
            await using var file = files["cities500.txt"];

            using var csv = new CsvReader(new StreamReader(file), _config);
            if (token.IsCancellationRequested)
                throw new TaskCanceledException("A task was cancelled.");

            return csv.GetRecords<GeoName>().ToList();
        }
        catch (TaskCanceledException ex)
        {
            throw new TaskCanceledException("A task was cancelled.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to get db from geonames.org.", ex);
        }
    }

    public async Task<IEnumerable<GeoName>> GetModificationsAsync(CancellationToken token = default)
    {
        try
        {
            _logger.LogInformation("GetModificationsAsync(...) was called.");
            var url = string.Format(_urlBase + "modifications-" + _date + ".txt");
            await using var file = await DownloadFileAsync(url, token);

            using var csv = new CsvReader(new StreamReader(file), _config);
            if (token.IsCancellationRequested)
                throw new TaskCanceledException("A task was cancelled.");

            return csv.GetRecords<GeoName>().ToList();
        }
        catch (TaskCanceledException ex)
        {
            throw new TaskCanceledException("A task was cancelled.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to get modifications from geonames.org.", ex);
        }
    }

    public async Task<IEnumerable<DeleteGeoName>> GetDeletesAsync(CancellationToken token = default)
    {
        try
        {
            _logger.LogInformation("GetDeletesAsync(...) was called.");
            var url = string.Format(_urlBase + "deletes-" + _date + ".txt");
            await using var file = await DownloadFileAsync(url, token);

            using var csv = new CsvReader(new StreamReader(file), _config);
            if (token.IsCancellationRequested)
                throw new TaskCanceledException("A task was cancelled.");

            return csv.GetRecords<DeleteGeoName>().ToList();
        }
        catch (TaskCanceledException ex)
        {
            throw new TaskCanceledException("A task was cancelled.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to get deletes from geonames.org.", ex);
        }
    }

    private async Task<Stream> DownloadFileAsync(string url, CancellationToken token = default)
    {
        try
        {
            _logger.LogInformation("DownloadFileAsync(...) was called.");
            Stream stream = new MemoryStream();
            using var client = new HttpClient();
            using var response =
                await client.GetAsync(url, token);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStreamAsync(token);
                await content.CopyToAsync(stream, token);
                stream.Position = 0;
                return stream;
            }

            throw new Exception($"Bad response status code {response.StatusCode}.");
        }
        catch (TaskCanceledException ex)
        {
            throw new TaskCanceledException("A task was cancelled.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to download file from {url}", ex);
        }
    }

    private static IDictionary<string, Stream> Unzip(Stream zip)
    {
        try
        {
            var files = new Dictionary<string, Stream>();

            using var archive = new ZipArchive(zip);
            foreach (var entry in archive.Entries)
            {
                var file = new MemoryStream();
                entry.Open().CopyTo(file);
                file.Position = 0;
                files.Add(entry.FullName, file);
            }

            return files;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to get files from zip.", ex);
        }
    }
}