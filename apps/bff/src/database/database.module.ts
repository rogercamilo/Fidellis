import { Global, Module } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Pool } from 'pg';

export const PG_POOL = 'PG_POOL';

/**
 * Pool Postgres compartilhado. O BFF autentica lendo o schema global `catalog`
 * (users/memberships); os dados por tenant ficam no core .NET.
 */
@Global()
@Module({
  providers: [
    {
      provide: PG_POOL,
      inject: [ConfigService],
      useFactory: (config: ConfigService) =>
        new Pool({
          host: config.get<string>('POSTGRES_HOST', 'localhost'),
          port: Number(config.get<string>('POSTGRES_PORT', '5432')),
          database: config.get<string>('POSTGRES_DB', 'fidellis'),
          user: config.get<string>('POSTGRES_USER', 'fidellis'),
          password: config.get<string>('POSTGRES_PASSWORD', 'fidellis_dev'),
          max: 10,
        }),
    },
  ],
  exports: [PG_POOL],
})
export class DatabaseModule {}
