-- Dados fiscais intrínsecos do produto
ALTER TABLE produto ADD COLUMN IF NOT EXISTS ncm VARCHAR(8);
ALTER TABLE produto ADD COLUMN IF NOT EXISTS cest VARCHAR(7);
ALTER TABLE produto ADD COLUMN IF NOT EXISTS origem INTEGER DEFAULT 0;

-- Catálogo de CFOPs por empresa
CREATE TABLE IF NOT EXISTS cfop (
    id SERIAL PRIMARY KEY,
    empresa_id INTEGER NOT NULL,
    codigo VARCHAR(4) NOT NULL,
    descricao TEXT,
    natureza_operacao VARCHAR(100),
    tipo_operacao VARCHAR(30) NOT NULL,
    ativo BOOLEAN DEFAULT TRUE,
    data_cadastro TIMESTAMP DEFAULT NOW(),
    UNIQUE(empresa_id, codigo)
);

-- Regras fiscais por produto e operação
CREATE TABLE IF NOT EXISTS produto_regra_fiscal (
    id SERIAL PRIMARY KEY,
    empresa_id INTEGER NOT NULL,
    produto_id INTEGER NOT NULL,
    cfop_id INTEGER NOT NULL,

    icms_cst VARCHAR(3),
    icms_aliquota DECIMAL(5,2),
    icms_reducao_bc DECIMAL(5,2),

    pis_cst VARCHAR(2),
    pis_aliquota DECIMAL(5,4),

    cofins_cst VARCHAR(2),
    cofins_aliquota DECIMAL(5,4),

    ipi_cst VARCHAR(2),
    ipi_aliquota DECIMAL(5,2),

    data_cadastro TIMESTAMP DEFAULT NOW(),
    UNIQUE(empresa_id, produto_id, cfop_id)
);
