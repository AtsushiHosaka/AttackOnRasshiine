-- This project is historical and must never be linked to a database. The
-- blocker sorts before every retained migration so a forced/accidental enable
-- fails before any historical schema change can commit.
do $$
begin
  raise exception using
    errcode = 'P0001',
    message = 'deprecated_backend_do_not_deploy';
end;
$$;
