START TRANSACTION;
WITH root_dir AS (
    INSERT INTO fsos (fso_name,virtual_location_id,permissions,fso_type)
    VALUES ( '/',NULL,0o755,'directory')
    RETURNING id
)
INSERT INTO users
    (username,password_hash,email,root,role)
    VALUES ('admin',digest('password','sha512'),'admin@example.org',(SELECT id from root_dir), 'admin');
COMMIT;
