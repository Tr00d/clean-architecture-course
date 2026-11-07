create table public.operations (
  id bigint generated always as identity primary key,
  account_id int not null,
  amount numeric not null,
  date timestamptz not null
);

alter table public.operations enable row level security;
