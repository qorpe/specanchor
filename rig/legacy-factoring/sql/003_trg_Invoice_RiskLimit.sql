-- Added 2011. Reason lost; kept because removing it broke month-end once.
CREATE OR ALTER TRIGGER dbo.trg_Invoice_RiskLimit
ON Invoice
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN Contract c ON c.ContractId = i.ContractId
        JOIN Customer cu ON cu.CustomerId = c.CustomerId
        WHERE i.Amount > cu.RiskLimit
    )
    BEGIN
        RAISERROR ('Invoice amount exceeds customer risk limit.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
