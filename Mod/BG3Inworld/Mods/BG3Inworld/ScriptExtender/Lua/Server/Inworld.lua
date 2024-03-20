Ext.Require("Server/Utils.lua")

FolderName = "BG3InworldData"

Ext.Utils.PrintWarning("Starting Inworld Mod within BG3")

local function testDataPrinting(cmd, a1, a2, ...)
    PrintData()
    Ext.Utils.PrintWarning("Data printing succesful. Check " .. FolderName .. "/data.json")
end

local function getCharacterGearSet(id)
    local helmetId = Osi.GetEquippedItem(id, "Helmet")
    local breastId = Osi.GetEquippedItem(id, "Breast")
    local meleeMainId = Osi.GetEquippedItem(id, "Melee Main Weapon")
    local reangedMainId = Osi.GetEquippedItem(id, "Ranged Main Weapon")
    return { Text.GetHumanReadableName(helmetId), Text.GetHumanReadableName(breastId),
        Text.GetHumanReadableName(meleeMainId), Text.GetHumanReadableName(reangedMainId) }
end

local function getPartyData()
    local res = {}
    for k, d in ipairs(Osi.DB_PartyMembers:Get(nil))
    do
        local id = d[1]
        local readableName = Text.GetHumanReadableName(id)
        local gearSet = getCharacterGearSet(id)
        table.insert(res, { id, readableName, gearSet })
    end
    return res
end

local function getLocation()
    local db = Osi.DB_CurrentLevel:Get(nil)
    if db == nil or #db == 0 then
        return nil
    end
    return db[1][1]
end


function PrintData()
    local x, y, z = Osi.GetPosition(Osi.GetHostCharacter())
    local logPath = "data.json"
    local location = { x, y, z }
    local fullFile = { location, getPartyData(), getLocation() }
    local stringContent = JSON.Stringify(fullFile)
    -- _P(stringContent)
    Files.Save(logPath, stringContent)
end

Ext.RegisterConsoleCommand("testInworldData", testDataPrinting);

-----------------------------------------------------------------------------------------------------
-----------------------------      Automatic Data Creation   ----------------------------------------
-----------------------------------------------------------------------------------------------------

local hGameStateChanged = 0
local locationCheckMs = 5000
local function timerChecker(finishedTimer)
    if finishedTimer == "BG3InworldTrigger" then
        PrintData()
        Osi.TimerLaunch("BG3InworldTrigger", locationCheckMs)
    end
end

local function _OnGameStateChange(e)
    if e.FromState == "Sync" and e.ToState == "Running" then
        Osi.TimerLaunch("BG3InworldTrigger", locationCheckMs)
        Ext.Events.GameStateChanged:Unsubscribe(hGameStateChanged)
    end
end

hGameStateChanged = Ext.Events.GameStateChanged:Subscribe(_OnGameStateChange)

Ext.Osiris.RegisterListener("TimerFinished", 1, "after", timerChecker)
