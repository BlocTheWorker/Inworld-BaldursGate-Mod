JSON = {}
Files = {}
Text = {}

function Text.GetHumanReadableName(id)
    if id == nil then return "" end
    return Osi.ResolveTranslatedString(Osi.GetDisplayName(id))
end

-- ------------------------------------------------------------------------------------------------------
-- File I/O Stuff credit to the kv camp event author and ABC, I basically just made their code/functions even worse
-- ------------------------------------------------------------------------------------------------------
function Files.Save(path, content)
    path = Files.Path(path)
    return Ext.IO.SaveFile(path, content)
end

function Files.Load(path)
    path = Files.Path(path)
    return Ext.IO.LoadFile(path)
end

function Files.Path(filePath)
    return FolderName .. "/" .. filePath
end

-- ------------------------------------------------------------------------------------------------------
-- JSON Functions
-- ------------------------------------------------------------------------------------------------------

function JSON.Parse(json_str)
    return Ext.Json.Parse(json_str)
end

function JSON.Stringify(data)
    return Ext.Json.Stringify(data)
end

-- ------------------------------------------------------------------------------------------------------
-- Some extra helper functions
-- ------------------------------------------------------------------------------------------------------

local function GetFloor(x)
    return x - x % 1
end

function GetTimestamp()
    local time = Ext.Utils.MonotonicTime()
    local milliseconds = time % 1000
    local seconds = GetFloor(time / 1000) % 60
    local minutes = GetFloor((time / 1000) / 60) % 60
    local hours = GetFloor(((time / 1000) / 60) / 60) % 24
    return string.format("%02d:%02d:%02d.%03d",
        hours, minutes, seconds, milliseconds)
end
