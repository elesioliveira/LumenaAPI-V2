-- Certificado digital da empresa
CREATE TABLE IF NOT EXISTS certificado_digital (
    id SERIAL PRIMARY KEY,
    empresa_id INTEGER NOT NULL UNIQUE,
    pfx_data BYTEA NOT NULL,
    senha_hash TEXT NOT NULL,
    razao_social TEXT,
    cnpj VARCHAR(14),
    validade_inicio TIMESTAMP,
    validade_fim TIMESTAMP,
    data_upload TIMESTAMP DEFAULT NOW()
);

-- Sequência de numeração NFe/NFCe por empresa e série
CREATE TABLE IF NOT EXISTS empresa_nfe_sequencia (
    id SERIAL PRIMARY KEY,
    empresa_id INTEGER NOT NULL,
    modelo INTEGER NOT NULL,
    serie INTEGER NOT NULL DEFAULT 1,
    ultimo_numero INTEGER NOT NULL DEFAULT 0,
    UNIQUE(empresa_id, modelo, serie)
);

-- Nota fiscal emitida
CREATE TABLE IF NOT EXISTS nota_fiscal (
    id SERIAL PRIMARY KEY,
    empresa_id INTEGER NOT NULL,
    modelo INTEGER NOT NULL,
    serie INTEGER NOT NULL,
    numero INTEGER NOT NULL,
    chave_acesso VARCHAR(44),
    natureza_operacao VARCHAR(100),
    data_emissao TIMESTAMP,
    data_autorizacao TIMESTAMP,

    dest_cpf_cnpj VARCHAR(14),
    dest_nome VARCHAR(200),
    dest_uf VARCHAR(2),

    valor_produtos DECIMAL(15,2),
    valor_total DECIMAL(15,2),
    valor_icms DECIMAL(15,2),
    valor_pis DECIMAL(15,2),
    valor_cofins DECIMAL(15,2),
    valor_ipi DECIMAL(15,2),
    valor_desconto DECIMAL(15,2),

    status VARCHAR(20) NOT NULL,
    protocolo_autorizacao VARCHAR(20),
    xml_envio TEXT,
    xml_retorno TEXT,
    xml_proc TEXT,
    motivo_rejeicao TEXT,
    ambiente INTEGER DEFAULT 2,

    venda_id INTEGER,

    data_cadastro TIMESTAMP DEFAULT NOW(),
    UNIQUE(empresa_id, modelo, serie, numero)
);

-- Itens da nota fiscal
CREATE TABLE IF NOT EXISTS nota_fiscal_item (
    id SERIAL PRIMARY KEY,
    nota_fiscal_id INTEGER NOT NULL,
    numero_item INTEGER NOT NULL,
    produto_id INTEGER,

    codigo VARCHAR(60),
    descricao VARCHAR(120),
    ncm VARCHAR(8),
    cest VARCHAR(7),
    cfop VARCHAR(4),
    unidade VARCHAR(6),
    quantidade DECIMAL(15,4),
    valor_unitario DECIMAL(15,10),
    valor_total DECIMAL(15,2),

    icms_origem INTEGER,
    icms_cst VARCHAR(3),
    icms_base_calculo DECIMAL(15,2),
    icms_aliquota DECIMAL(5,2),
    icms_valor DECIMAL(15,2),

    pis_cst VARCHAR(2),
    pis_base_calculo DECIMAL(15,2),
    pis_aliquota DECIMAL(5,4),
    pis_valor DECIMAL(15,2),

    cofins_cst VARCHAR(2),
    cofins_base_calculo DECIMAL(15,2),
    cofins_aliquota DECIMAL(5,4),
    cofins_valor DECIMAL(15,2),

    ipi_cst VARCHAR(2),
    ipi_base_calculo DECIMAL(15,2),
    ipi_aliquota DECIMAL(5,2),
    ipi_valor DECIMAL(15,2)
);

-- Campos fiscais na empresa
ALTER TABLE empresa ADD COLUMN IF NOT EXISTS csc_id VARCHAR(6);
ALTER TABLE empresa ADD COLUMN IF NOT EXISTS csc_token VARCHAR(36);
ALTER TABLE empresa ADD COLUMN IF NOT EXISTS nfe_ambiente INTEGER DEFAULT 2;
ALTER TABLE empresa ADD COLUMN IF NOT EXISTS uf VARCHAR(2);
ALTER TABLE empresa ADD COLUMN IF NOT EXISTS regime_tributario INTEGER DEFAULT 1;
