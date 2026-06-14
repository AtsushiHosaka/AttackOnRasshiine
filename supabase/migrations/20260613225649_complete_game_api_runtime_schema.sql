alter table public.teams
    add column if not exists code text;

update public.teams
set code = lower(regexp_replace(name, '\s+', '_', 'g'))
where code is null;

alter table public.teams
    alter column code set not null;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'teams_code_unique'
          and conrelid = 'public.teams'::regclass
    ) then
        alter table public.teams
            add constraint teams_code_unique unique (code);
    end if;
end;
$$;

alter table public.character_stats
    add column if not exists unlocked_weapons jsonb not null default '[0]'::jsonb,
    add column if not exists titles jsonb not null default '[]'::jsonb,
    add column if not exists skills jsonb not null default '["基礎攻撃"]'::jsonb;

create table if not exists public.audit_logs (
    id uuid primary key default gen_random_uuid(),
    actor_user_id uuid references public.users(id) on delete set null,
    action_type text not null,
    target_type text not null,
    target_id uuid,
    before_state text,
    after_state text,
    created_at timestamptz not null default now(),
    constraint audit_logs_action_type_not_blank check (length(trim(action_type)) > 0),
    constraint audit_logs_target_type_not_blank check (length(trim(target_type)) > 0)
);

alter table public.audit_logs
    add column if not exists before_state text,
    add column if not exists after_state text;

alter table public.audit_logs enable row level security;

create index if not exists audit_logs_target_idx
    on public.audit_logs (target_type, target_id, created_at desc);

alter table public.boss_battles
    add column if not exists boss_id text,
    add column if not exists boss_def integer not null default 4,
    add column if not exists story_teaser text not null default 'なぜメンターが襲ってくるのか。次の勝利で記録断片が解放される。',
    add column if not exists turn_number integer not null default 1,
    add column if not exists phase text not null default 'turn_start',
    add column if not exists total_damage integer not null default 0,
    add column if not exists highlight_user_id uuid references public.users(id) on delete set null,
    add column if not exists started_at timestamptz,
    add column if not exists completed_at timestamptz;

create table if not exists public.battle_participants (
    battle_id uuid not null references public.boss_battles(id) on delete cascade,
    user_id uuid not null references public.users(id) on delete cascade,
    nickname text not null,
    role public.battle_role not null default 'attacker',
    weapon_kind text not null default 'blade',
    current_hp integer not null default 100,
    current_mp integer not null default 30,
    total_damage integer not null default 0,
    total_heal integer not null default 0,
    support_count integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (battle_id, user_id),
    constraint battle_participants_current_hp_non_negative check (current_hp >= 0),
    constraint battle_participants_current_mp_non_negative check (current_mp >= 0),
    constraint battle_participants_total_damage_non_negative check (total_damage >= 0),
    constraint battle_participants_total_heal_non_negative check (total_heal >= 0),
    constraint battle_participants_support_count_non_negative check (support_count >= 0)
);

alter table public.battle_participants enable row level security;

drop trigger if exists battle_participants_set_updated_at on public.battle_participants;

create trigger battle_participants_set_updated_at
    before update on public.battle_participants
    for each row execute function public.set_updated_at();

alter table public.battle_actions
    add column if not exists weapon_kind text not null default 'blade',
    add column if not exists message text,
    add column if not exists heal integer not null default 0,
    add column if not exists support_effect jsonb;

do $$
begin
    insert into public.teams (code, name)
    values
        ('blue', 'ブルー班'),
        ('magenta', 'マゼンタ班'),
        ('mentor', 'メンター')
    on conflict (code) do nothing;
end;
$$;

grant usage on schema public to service_role;
grant select, insert, update, delete on all tables in schema public to service_role;
grant usage, select on all sequences in schema public to service_role;

alter default privileges in schema public
    grant select, insert, update, delete on tables to service_role;

alter default privileges in schema public
    grant usage, select on sequences to service_role;

drop index if exists public.audit_logs_actor_idx;
drop index if exists public.ai_evaluations_dev_session_id_idx;

do $$
begin
    if exists (
        select 1
        from pg_constraint
        where conname = 'ai_evaluations_dev_session_id_key'
          and conrelid = 'public.ai_evaluations'::regclass
    )
    and exists (
        select 1
        from pg_constraint
        where conname = 'ai_evaluations_dev_session_id_unique'
          and conrelid = 'public.ai_evaluations'::regclass
    ) then
        alter table public.ai_evaluations
            drop constraint ai_evaluations_dev_session_id_unique;
    end if;
end;
$$;

do $$
begin
    if exists (select 1 from pg_views where schemaname = 'public' and viewname = 'member_rankings') then
        alter view public.member_rankings set (security_invoker = true);
    end if;
    if exists (select 1 from pg_views where schemaname = 'public' and viewname = 'weekly_dev_rankings') then
        alter view public.weekly_dev_rankings set (security_invoker = true);
    end if;
    if exists (select 1 from pg_views where schemaname = 'public' and viewname = 'team_weekly_rankings') then
        alter view public.team_weekly_rankings set (security_invoker = true);
    end if;
end;
$$;

do $$
declare
    function_signature text;
    function_ref regprocedure;
begin
    foreach function_signature in array array[
        'public.approve_dev_session(uuid, uuid, integer, text)',
        'public.authenticate_app_user(text, text)',
        'public.complete_dev_session(uuid, uuid, integer, text, text)',
        'public.ensure_member_character_stats()',
        'public.grant_exp_to_member(uuid, integer)',
        'public.mark_incomplete_sessions()',
        'public.reject_dev_session(uuid, uuid, text)',
        'public.start_dev_session(uuid, text)',
        'public.submit_battle_action(uuid, uuid, integer, public.battle_role, uuid, public.battle_action_type)'
    ] loop
        function_ref := to_regprocedure(function_signature);
        if function_ref is not null then
            execute format('revoke execute on function %s from anon, authenticated, public', function_ref);
        end if;
    end loop;
end;
$$;

do $$
declare
    function_signature text;
    function_ref regprocedure;
begin
    foreach function_signature in array array[
        'public.set_updated_at()',
        'public.ai_rank_multiplier(public.ai_rank)',
        'public.required_exp_for_level(integer)',
        'public.calculate_suspicious_flags(uuid, integer, text, integer, text, text, timestamp with time zone)',
        'public.assert_mentor(uuid)',
        'public.recalculate_character_stats_from_level()'
    ] loop
        function_ref := to_regprocedure(function_signature);
        if function_ref is not null then
            execute format('alter function %s set search_path = public, pg_temp', function_ref);
        end if;
    end loop;
end;
$$;
