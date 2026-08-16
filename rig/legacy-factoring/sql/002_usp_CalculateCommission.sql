-- Nightly commission calculation. Do not modify without approval from pricing desk.
CREATE OR ALTER PROCEDURE dbo.usp_CalculateCommission
    @InvoiceId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Amount DECIMAL(18,2), @Rate DECIMAL(9,6),
            @MinCommission DECIMAL(18,2), @ContractType INT,
            @Commission DECIMAL(18,2);

    SELECT @Amount = i.Amount,
           @Rate = c.CommissionRate,
           @MinCommission = c.MinCommission,
           @ContractType = c.ContractType
    FROM Invoice i
    JOIN Contract c ON c.ContractId = i.ContractId
    WHERE i.InvoiceId = @InvoiceId;

    SET @Commission = ROUND(@Amount * @Rate, 2);

    IF @Commission < @MinCommission AND @ContractType <> 3
        SET @Commission = @MinCommission;

    INSERT INTO CommissionResult (InvoiceId, Commission)
    VALUES (@InvoiceId, @Commission);
END
