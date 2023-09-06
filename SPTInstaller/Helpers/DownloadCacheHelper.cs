﻿using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using SPTInstaller.Models;

namespace SPTInstaller.Helpers;

public static class DownloadCacheHelper
{
    private static HttpClient _httpClient = new() { Timeout = TimeSpan.FromHours(1) };

    public static string CachePath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spt-installer/cache");

    public static string GetCacheSizeText()
    {
        if (!Directory.Exists(CachePath))
            return "No cache folder";

        var cacheDir = new DirectoryInfo(CachePath);

        var cacheSize = DirectorySizeHelper.GetSizeOfDirectory(cacheDir);

        if (cacheSize == 0)
            return "Empty";

        return DirectorySizeHelper.SizeSuffix(cacheSize);
    }

    private static bool CheckCache(FileInfo cacheFile, string expectedHash = null)
    {
        try
        {
            cacheFile.Refresh();
            Directory.CreateDirectory(CachePath);

            if (cacheFile.Exists)
            {
                if (expectedHash != null && FileHashHelper.CheckHash(cacheFile, expectedHash))
                {
                    Log.Information($"Using cached file: {cacheFile.Name} - Hash: {expectedHash}");
                    return true;
                }

                cacheFile.Delete();
                cacheFile.Refresh();
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Result> DownloadFile(FileInfo outputFile, string targetLink, IProgress<double> progress, string expectedHash = null)
    {
        try
        {
            // Use the provided extension method
            using (var file = new FileStream(outputFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
                await _httpClient.DownloadDataAsync(targetLink, file, progress);

            outputFile.Refresh();

            if (!outputFile.Exists)
            {
                return Result.FromError($"Failed to download {outputFile.Name}");
            }

            if (expectedHash != null && !FileHashHelper.CheckHash(outputFile, expectedHash))
            {
                return Result.FromError("Hash mismatch");
            }

            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromError(ex.Message);
        }
    }

    private static async Task<Result> ProcessInboundStreamAsync(FileInfo cacheFile, Stream downloadStream, string expectedHash = null)
    {
        try
        {
            if (CheckCache(cacheFile, expectedHash)) return Result.FromSuccess();

            using var patcherFileStream = cacheFile.Open(FileMode.Create);
            {
                await downloadStream.CopyToAsync(patcherFileStream);
            }

            patcherFileStream.Close();

            if (expectedHash != null && !FileHashHelper.CheckHash(cacheFile, expectedHash))
            {
                return Result.FromError("Hash mismatch");
            }

            return Result.FromSuccess();
        }
        catch(Exception ex)
        {
            return Result.FromError(ex.Message);
        }
    }

    private static async Task<Result> ProcessInboundFileAsync(FileInfo cacheFile, string targetLink, IProgress<double> progress, string expectedHash = null)
    {
        try
        {
            if (CheckCache(cacheFile, expectedHash)) return Result.FromSuccess();

            return await DownloadFile(cacheFile, targetLink, progress, expectedHash);
        }
        catch(Exception ex)
        {
            return Result.FromError(ex.Message);
        }
    }

    public static async Task<FileInfo?> GetOrDownloadFileAsync(string fileName, string targetLink, IProgress<double> progress, string expectedHash = null)
    {
        var cacheFile = new FileInfo(Path.Join(CachePath, fileName));

        try
        {
            var result = await ProcessInboundFileAsync(cacheFile, targetLink, progress, expectedHash);

            return result.Succeeded ? cacheFile : null;
        }
        catch(Exception ex)
        {
            Log.Error(ex, $"Error while getting file: {fileName}");
            return null;
        }
    }

    public static async Task<FileInfo?> GetOrDownloadFileAsync(string fileName, Stream fileDownloadStream, string expectedHash = null)
    {
        var cacheFile = new FileInfo(Path.Join(CachePath, fileName));

        try
        {
            var result = await ProcessInboundStreamAsync(cacheFile, fileDownloadStream, expectedHash);

            return result.Succeeded ? cacheFile : null;
        }
        catch(Exception ex)
        {
            Log.Error(ex, $"Error while getting file: {fileName}");
            return null;
        }
    }
}