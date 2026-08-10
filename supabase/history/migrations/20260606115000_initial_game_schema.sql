create extension if not exists pgcrypto;

create type public.user_role as enum ('member', 'mentor');
create type public.dev_session_status as enum ('in_progress', 'pending', 'approved', 'rejected', 'incomplete', 'needs_review', 'ai_pending');
create type public.ai_rank as enum ('S', 'A+', 'A', 'B', 'C', 'D');
create type public.battle_status as enum ('scheduled', 'active', 'completed');
create type public.battle_result as enum ('win', 'lose');
create type public.battle_role as enum ('attacker', 'healer', 'defender', 'supporter');
create type public.battle_action_type as enum ('normal', 'strong', 'full_power', 'support', 'guard');
create type public.achievement_type as enum ('contest_submission', 'release', 'update', 'award', 'continuous_dev');
create type public.achievement_status as enum ('pending', 'approved', 'rejected');

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

create table public.teams (
    id uuid primary key default gen_random_uuid(),
    name text not null unique,
    mentor_user_id uuid,
    created_at timestamptz not null default now(),
    constraint teams_name_not_blank check (length(trim(name)) > 0)
);

create table public.users (
    id uuid primary key default gen_random_uuid(),
    login_id text not null,
    password_hash text not null,
    nickname text not null,
    role public.user_role not null,
    team_id uuid references public.teams(id) on delete set null,
    ranking_visible boolean not null default true,
    initial_password_changed boolean not null default false,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint users_login_id_not_blank check (length(trim(login_id)) >= 3),
    constraint users_password_hash_not_blank check (length(trim(password_hash)) > 0),
    constraint users_nickname_not_blank check (length(trim(nickname)) > 0)
);

alter table public.teams
    add constraint teams_mentor_user_id_fkey
    foreign key (mentor_user_id) references public.users(id) on delete set null;

create table public.dev_sessions (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references public.users(id) on delete cascade,
    started_at timestamptz not null,
    ended_at timestamptz,
    duration_minutes integer,
    goal text not null,
    achievement_rate integer,
    reflection text,
    next_task text,
    status public.dev_session_status not null default 'in_progress',
    suspicious_flags jsonb not null default '[]'::jsonb,
    mentor_comment text,
    approved_by uuid references public.users(id) on delete set null,
    approved_at timestamptz,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint dev_sessions_goal_not_blank check (length(trim(goal)) > 0),
    constraint dev_sessions_duration_non_negative check (duration_minutes is null or duration_minutes >= 0),
    constraint dev_sessions_achievement_rate_range check (achievement_rate is null or achievement_rate between 0 and 100),
    constraint dev_sessions_ended_after_start check (ended_at is null or ended_at >= started_at)
);

create table public.ai_evaluations (
    id uuid primary key default gen_random_uuid(),
    dev_session_id uuid not null references public.dev_sessions(id) on delete cascade,
    total_score integer not null,
    rank public.ai_rank not null,
    axis_scores jsonb not null default '{}'::jsonb,
    feedback text not null,
    exp_multiplier numeric(6, 3) not null default 1,
    model_name text not null,
    created_at timestamptz not null default now(),
    constraint ai_evaluations_total_score_range check (total_score between 0 and 100),
    constraint ai_evaluations_exp_multiplier_positive check (exp_multiplier > 0),
    constraint ai_evaluations_feedback_not_blank check (length(trim(feedback)) > 0),
    constraint ai_evaluations_model_name_not_blank check (length(trim(model_name)) > 0)
);

create table public.character_stats (
    user_id uuid primary key references public.users(id) on delete cascade,
    level integer not null default 1,
    exp integer not null default 0,
    hp integer not null default 100,
    atk integer not null default 10,
    def integer not null default 5,
    mp integer not null default 30,
    updated_at timestamptz not null default now(),
    constraint character_stats_level_positive check (level >= 1),
    constraint character_stats_exp_non_negative check (exp >= 0),
    constraint character_stats_hp_positive check (hp > 0),
    constraint character_stats_atk_non_negative check (atk >= 0),
    constraint character_stats_def_non_negative check (def >= 0),
    constraint character_stats_mp_non_negative check (mp >= 0)
);

create table public.boss_battles (
    id uuid primary key default gen_random_uuid(),
    week_start_date date not null,
    boss_name text not null,
    boss_type text not null,
    base_hp integer not null,
    hp_multiplier numeric(6, 3) not null default 1,
    current_hp integer not null,
    max_hp integer not null,
    turn_count integer not null,
    status public.battle_status not null default 'scheduled',
    result public.battle_result,
    created_by uuid not null references public.users(id) on delete restrict,
    created_at timestamptz not null default now(),
    constraint boss_battles_boss_name_not_blank check (length(trim(boss_name)) > 0),
    constraint boss_battles_boss_type_not_blank check (length(trim(boss_type)) > 0),
    constraint boss_battles_base_hp_positive check (base_hp > 0),
    constraint boss_battles_hp_multiplier_positive check (hp_multiplier > 0),
    constraint boss_battles_current_hp_range check (current_hp >= 0 and current_hp <= max_hp),
    constraint boss_battles_max_hp_positive check (max_hp > 0),
    constraint boss_battles_turn_count_positive check (turn_count > 0)
);

create table public.battle_actions (
    id uuid primary key default gen_random_uuid(),
    battle_id uuid not null references public.boss_battles(id) on delete cascade,
    user_id uuid not null references public.users(id) on delete cascade,
    turn_number integer not null,
    role public.battle_role not null,
    weapon_id uuid,
    action_type public.battle_action_type not null,
    mp_cost integer not null default 0,
    damage integer not null default 0,
    heal integer not null default 0,
    support_effect jsonb,
    created_at timestamptz not null default now(),
    constraint battle_actions_turn_number_positive check (turn_number >= 1),
    constraint battle_actions_mp_cost_non_negative check (mp_cost >= 0),
    constraint battle_actions_damage_non_negative check (damage >= 0),
    constraint battle_actions_heal_non_negative check (heal >= 0),
    constraint battle_actions_one_action_per_turn unique (battle_id, user_id, turn_number)
);

create table public.achievements (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references public.users(id) on delete cascade,
    type public.achievement_type not null,
    title text not null,
    description text,
    status public.achievement_status not null default 'pending',
    approved_by uuid references public.users(id) on delete set null,
    approved_at timestamptz,
    created_at timestamptz not null default now(),
    constraint achievements_title_not_blank check (length(trim(title)) > 0)
);

create table public.products (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references public.users(id) on delete cascade,
    title text not null,
    url text not null,
    description text,
    is_public boolean not null default true,
    hidden_by uuid references public.users(id) on delete set null,
    created_at timestamptz not null default now(),
    constraint products_title_not_blank check (length(trim(title)) > 0),
    constraint products_url_not_blank check (length(trim(url)) > 0)
);

create trigger users_set_updated_at
    before update on public.users
    for each row execute function public.set_updated_at();

create trigger dev_sessions_set_updated_at
    before update on public.dev_sessions
    for each row execute function public.set_updated_at();

create trigger character_stats_set_updated_at
    before update on public.character_stats
    for each row execute function public.set_updated_at();

create unique index users_login_id_unique_idx on public.users (lower(login_id));
create index users_team_id_idx on public.users (team_id);
create index dev_sessions_user_status_idx on public.dev_sessions (user_id, status, started_at desc);
create index ai_evaluations_dev_session_id_idx on public.ai_evaluations (dev_session_id);
create index boss_battles_week_start_date_idx on public.boss_battles (week_start_date desc);
create index battle_actions_battle_turn_idx on public.battle_actions (battle_id, turn_number);
create index achievements_user_status_idx on public.achievements (user_id, status, created_at desc);
create index products_user_created_at_idx on public.products (user_id, created_at desc);

alter table public.teams enable row level security;
alter table public.users enable row level security;
alter table public.dev_sessions enable row level security;
alter table public.ai_evaluations enable row level security;
alter table public.character_stats enable row level security;
alter table public.boss_battles enable row level security;
alter table public.battle_actions enable row level security;
alter table public.achievements enable row level security;
alter table public.products enable row level security;
