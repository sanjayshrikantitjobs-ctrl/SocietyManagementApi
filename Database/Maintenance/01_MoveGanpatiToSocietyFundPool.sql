-- One-time data correction: Ganpati Festival 2026 (Id 3) predates the
-- Pool/Child festival model and was carrying its own real contribution
-- data. Society Fund 2026 (Id 5) is the real yearly pool going forward, so
-- Ganpati's real collected data moves there, and Ganpati becomes a Child of
-- the pool. Society Fund's own contribution rows are test data from this
-- session's feature verification and are cleared first to avoid a
-- (FestivalId, FlatId) collision with Ganpati's real targets.
--
-- Run once. Not idempotent-safe to re-run against a database that has
-- already had this applied (FestivalId = 3 will have no rows left).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

DELETE FROM FestivalContributions WHERE FestivalId = 5;
DELETE FROM FestivalFlatTargets WHERE FestivalId = 5;

UPDATE FestivalContributions SET FestivalId = 5 WHERE FestivalId = 3;
UPDATE FestivalFlatTargets SET FestivalId = 5 WHERE FestivalId = 3;

UPDATE Festivals SET Kind = 3, ContributionPoolFestivalId = 5 WHERE Id = 3;

COMMIT TRANSACTION;

SELECT Id, Name, Kind, ContributionPoolFestivalId FROM Festivals WHERE Id IN (3, 5);
