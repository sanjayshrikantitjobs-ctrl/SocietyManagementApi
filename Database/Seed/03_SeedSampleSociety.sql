/* =============================================================================
   OPTIONAL sample data for local development so the UI has something to show
   immediately. Safe to skip entirely in staging/production — nothing else in
   the schema depends on this data existing. Idempotent.
   ============================================================================= */

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Societies WHERE Name = 'Green Valley Co-operative Housing Society')
BEGIN
    INSERT INTO dbo.Societies (Name, RegistrationNumber, Address, City, State, Pincode, ContactEmail, ContactPhone, EstablishedDate, CreatedBy)
    VALUES ('Green Valley Co-operative Housing Society', 'GVCHS/2010/00123', '221 Lakeview Road', 'Pune', 'Maharashtra', '411045', 'office@greenvalley.example', '+91-9800000000', '2010-06-15', 'system');
END
GO

DECLARE @SocietyId INT = (SELECT TOP 1 Id FROM dbo.Societies WHERE Name = 'Green Valley Co-operative Housing Society');

IF NOT EXISTS (SELECT 1 FROM dbo.Buildings WHERE SocietyId = @SocietyId AND Name = 'Building A')
    INSERT INTO dbo.Buildings (SocietyId, Name, Description, CreatedBy) VALUES (@SocietyId, 'Building A', 'Main tower', 'system');

DECLARE @BuildingId INT = (SELECT TOP 1 Id FROM dbo.Buildings WHERE SocietyId = @SocietyId AND Name = 'Building A');

IF NOT EXISTS (SELECT 1 FROM dbo.Wings WHERE BuildingId = @BuildingId AND Name = 'Wing 1')
    INSERT INTO dbo.Wings (BuildingId, Name, CreatedBy) VALUES (@BuildingId, 'Wing 1', 'system');

DECLARE @WingId INT = (SELECT TOP 1 Id FROM dbo.Wings WHERE BuildingId = @BuildingId AND Name = 'Wing 1');

IF NOT EXISTS (SELECT 1 FROM dbo.Floors WHERE WingId = @WingId AND FloorNumber = 1)
    INSERT INTO dbo.Floors (WingId, FloorNumber, Name, CreatedBy) VALUES (@WingId, 1, 'First Floor', 'system');

DECLARE @FloorId INT = (SELECT TOP 1 Id FROM dbo.Floors WHERE WingId = @WingId AND FloorNumber = 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Flats WHERE FloorId = @FloorId AND FlatNumber = 'A-101')
    INSERT INTO dbo.Flats (FloorId, FlatNumber, FlatType, AreaSqFt, Status, CreatedBy) VALUES (@FloorId, 'A-101', 3, 950.00, 1, 'system');

IF NOT EXISTS (SELECT 1 FROM dbo.ParkingSlots WHERE SocietyId = @SocietyId AND SlotNumber = 'P-01')
    INSERT INTO dbo.ParkingSlots (SocietyId, SlotNumber, Type, Status, CreatedBy) VALUES (@SocietyId, 'P-01', 2, 1, 'system');

PRINT 'Sample society structure seeded successfully.';
