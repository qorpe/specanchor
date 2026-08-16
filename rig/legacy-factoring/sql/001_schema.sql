-- Legacy factoring rig — schema
CREATE TABLE Customer (
    CustomerId      INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(200) NOT NULL,
    RiskLimit       DECIMAL(18,2) NOT NULL
);

CREATE TABLE Contract (
    ContractId      INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId      INT NOT NULL REFERENCES Customer(CustomerId),
    ContractType    INT NOT NULL,              -- 1: yurtici kabil-i rucu, 2: yurtici gayri kabil-i rucu, 3: ???
    CommissionRate  DECIMAL(9,6) NOT NULL,
    MinCommission   DECIMAL(18,2) NOT NULL
);

CREATE TABLE Invoice (
    InvoiceId       INT IDENTITY(1,1) PRIMARY KEY,
    ContractId      INT NOT NULL REFERENCES Contract(ContractId),
    Amount          DECIMAL(18,2) NOT NULL,
    DueDate         DATE NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'created'
);

CREATE TABLE CommissionResult (
    ResultId        INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL REFERENCES Invoice(InvoiceId),
    Commission      DECIMAL(18,2) NOT NULL,
    CalculatedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE TemlikKayit (
    TemlikId        INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL REFERENCES Invoice(InvoiceId),
    SupplierId      INT NOT NULL,
    IhbarTarihi     DATE NOT NULL
);
