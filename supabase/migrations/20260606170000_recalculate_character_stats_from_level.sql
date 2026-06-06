create or replace function public.recalculate_character_stats_from_level()
returns trigger
language plpgsql
as $$
begin
    new.level = greatest(1, new.level);
    new.exp = greatest(0, new.exp);
    new.hp = 100 + (new.level - 1) * 10;
    new.atk = 10 + (new.level - 1) * 2;
    new.def = 5 + (new.level - 1);
    new.mp = 30 + (new.level - 1) * 2;
    return new;
end;
$$;

drop trigger if exists character_stats_recalculate_derived_stats on public.character_stats;

create trigger character_stats_recalculate_derived_stats
    before insert or update of level, exp, hp, atk, def, mp
    on public.character_stats
    for each row
    execute function public.recalculate_character_stats_from_level();

update public.character_stats
set level = greatest(1, level),
    exp = greatest(0, exp);
