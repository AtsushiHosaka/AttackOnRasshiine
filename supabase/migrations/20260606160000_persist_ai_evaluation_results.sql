alter table public.dev_sessions
    add column if not exists ai_evaluation_failure_reason text;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ai_evaluations_dev_session_id_unique'
          and conrelid = 'public.ai_evaluations'::regclass
    ) then
        alter table public.ai_evaluations
            add constraint ai_evaluations_dev_session_id_unique unique (dev_session_id);
    end if;
end;
$$;

create index if not exists ai_evaluations_rank_created_at_idx
    on public.ai_evaluations (rank, created_at desc);
