-- A sold-out departure has zero remaining seats. Keep the nonnegative constraint.
BEGIN;
ALTER TABLE public.tour_departures
    DROP CONSTRAINT IF EXISTS tour_departures_available_slots_check;
ALTER TABLE public.tour_departures
    ADD CONSTRAINT tour_departures_available_slots_check CHECK (available_slots >= 0);
COMMIT;
