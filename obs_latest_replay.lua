obs = obslua

source_name = "Media"

function update_media_source()
    if not source_name or source_name == "" then
        source_name = "Media"
    end

    -- Fast native OBS frontend call (0ms delay, no CMD shell process, 100% instant)
    local replay_path = obs.obs_frontend_get_last_replay()
    if replay_path ~= nil and replay_path ~= "" then
        local source = obs.obs_get_source_by_name(source_name)
        if source ~= nil then
            local settings = obs.obs_data_create()
            obs.obs_data_set_string(settings, "local_file", replay_path)
            obs.obs_source_update(source, settings)
            obs.obs_data_release(settings)
            obs.obs_source_release(source)
        end
    end
end

function on_event(event)
    if event == obs.OBS_FRONTEND_EVENT_REPLAY_BUFFER_SAVED then
        update_media_source()
    end
end

function script_load(settings)
    obs.obs_frontend_add_event_callback(on_event)
end

function script_description()
    return "Replay Buffer saqlanganda saqlangan video faylni 0ms kechikishsiz (tezkor va qotishlarsiz) Media manbasiga biriktiradi."
end

function script_properties()
    local props = obs.obs_properties_create()
    
    local p = obs.obs_properties_add_list(props, "source_name", "Media Manbasi (Source)", obs.OBS_COMBO_TYPE_LIST, obs.OBS_COMBO_FORMAT_STRING)
    local sources = obs.obs_enum_sources()
    if sources ~= nil then
        for _, source in ipairs(sources) do
            local name = obs.obs_source_get_name(source)
            obs.obs_property_list_add_string(p, name, name)
        end
        obs.source_list_release(sources)
    end

    return props
end

function script_defaults(settings)
    obs.obs_data_set_default_string(settings, "source_name", "Media")
end

function script_update(settings)
    source_name = obs.obs_data_get_string(settings, "source_name")
end
