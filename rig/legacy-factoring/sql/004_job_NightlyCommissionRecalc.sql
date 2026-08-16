-- SQL Agent job definition (exported). Runs 02:00 on weekdays.
-- Step 1: recalculate commissions for all invoices still in 'created' status.
DECLARE @InvoiceId INT;

DECLARE invoice_cursor CURSOR FOR
    SELECT InvoiceId FROM Invoice WHERE Status = 'created';

OPEN invoice_cursor;
FETCH NEXT FROM invoice_cursor INTO @InvoiceId;

WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC dbo.usp_CalculateCommission @InvoiceId = @InvoiceId;
    FETCH NEXT FROM invoice_cursor INTO @InvoiceId;
END

CLOSE invoice_cursor;
DEALLOCATE invoice_cursor;
