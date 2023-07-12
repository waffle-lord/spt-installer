﻿using SPTInstaller.Interfaces;
using SPTInstaller.Models;
using System.Threading.Tasks;
using SPTInstaller.Helpers;

namespace SPTInstaller.Installer_Tasks;

public class InitializationTask : InstallerTaskBase
{
    private InternalData _data;

    public InitializationTask(InternalData data) : base("Startup")
    {
        _data = data;
    }

    public override async Task<IResult> TaskOperation()
    {
        SetStatus("Initializing", $"Target Install Path: {FileHelper.GetRedactedPath(_data.TargetInstallPath)}");

        _data.OriginalGamePath = PreCheckHelper.DetectOriginalGamePath();

        if (_data.OriginalGamePath == null)
        {
            return Result.FromError("EFT IS NOT INSTALLED!");
        }

        SetStatus(null, $"Installed EFT Game Path: {FileHelper.GetRedactedPath(_data.OriginalGamePath)}");

        var result = PreCheckHelper.DetectOriginalGameVersion(_data.OriginalGamePath);

        if (!result.Succeeded)
        {
            return result;
        }

        _data.OriginalGameVersion = result.Message;

        SetStatus(null, $"Installed EFT Game Version: {_data.OriginalGameVersion}");

        if (_data.OriginalGamePath == null)
        {
            return Result.FromError("Unable to find original EFT directory, please make sure EFT is installed. Please also run EFT once");
        }

        if (_data.OriginalGamePath == _data.TargetInstallPath)
        {
            return Result.FromError("Installer is located in EFT's original directory. Please move the installer to a seperate folder as per the guide");
        }

        if (File.Exists(Path.Join(_data.TargetInstallPath, "EscapeFromTarkov.exe")))
        {
            return Result.FromError("Installer is located in a folder that has existing game files. Please make sure the installer is in a fresh folder as per the guide");
        }

        return Result.FromSuccess($"Current Game Version: {_data.OriginalGameVersion}");
    }
}