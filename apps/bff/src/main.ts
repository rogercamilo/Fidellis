import 'reflect-metadata';
import { ValidationPipe } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);

  app.useGlobalPipes(new ValidationPipe({ whitelist: true, transform: true }));

  const webOrigin = process.env.WEB_ORIGIN ?? 'http://localhost:3000';
  app.enableCors({ origin: webOrigin, credentials: true });

  const port = Number(process.env.BFF_PORT ?? 4000);
  await app.listen(port);
  console.log(`Fidellis BFF ouvindo em http://localhost:${port}`);
}

void bootstrap();
