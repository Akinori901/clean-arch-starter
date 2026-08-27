<?php

declare(strict_types=1);

use App\Http\Controllers\AuthController;
use App\Http\Controllers\HealthController;
use Illuminate\Support\Facades\Route;

Route::get('/health', [HealthController::class, 'index']);
Route::get('/health/live', [HealthController::class, 'live']);
Route::post('/auth/sign-in', [AuthController::class, 'signIn']);
Route::get('/auth/me', [AuthController::class, 'me']);
