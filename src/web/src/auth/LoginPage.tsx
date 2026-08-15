import { type ChangeEvent, type FC, type FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/design-system/Button';
import { Card, Surface } from '@/design-system/Surface';
import { Field, Input } from '@/design-system/Field';
import { Stack } from '@/design-system/Layout';
import { useAuth } from './useAuth';

export const LoginPage: FC = () => {
  const navigate = useNavigate();
  const { login, error: authError } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!email.trim()) {
      newErrors.email = 'Email is required';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      newErrors.email = 'Please enter a valid email address';
    }

    if (!password) {
      newErrors.password = 'Credential is required';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();

    if (!validateForm()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await login(email, password);
      navigate('/dashboard', { replace: true });
    } catch {
      // Error is handled by the auth context and displayed via authError
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 px-4 py-12 dark:bg-gray-950">
      <div className="w-full max-w-md">
        <Card>
          <Stack className="gap-6">
            <div>
              <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-white">
                Project Chicago
              </h1>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                Sign in to your account to continue
              </p>
            </div>

            <form onSubmit={handleSubmit} noValidate className="space-y-5">
              <Field
                label="Email Address"
                error={errors.email}
                required
              >
                <Input
                  id="email"
                  type="email"
                  name="email"
                  value={email}
                  onChange={(e: ChangeEvent<HTMLInputElement>) => {
                    setEmail(e.target.value);
                    if (errors.email) {
                      setErrors({ ...errors, email: '' });
                    }
                  }}
                  placeholder="name@example.com"
                  disabled={isSubmitting}
                  invalid={!!errors.email}
                  autoComplete="email"
                />
              </Field>

              <Field
                label="Password"
                error={errors.password}
                required
              >
                <Input
                  id="password"
                  type="password"
                  name="password"
                  value={password}
                  onChange={(e: ChangeEvent<HTMLInputElement>) => {
                    setPassword(e.target.value);
                    if (errors.password) {
                      setErrors({ ...errors, password: '' });
                    }
                  }}
                  placeholder="Enter your password"
                  disabled={isSubmitting}
                  invalid={!!errors.password}
                  autoComplete="current-password"
                />
              </Field>

              {authError && (
                <Surface
                  className="border-error-200 bg-error-50 p-4 dark:border-error-800 dark:bg-error-950"
                  radius="lg"
                  role="alert"
                >
                  <p className="text-sm text-error-800 dark:text-error-200">
                    {authError}
                  </p>
                </Surface>
              )}

              <Button
                type="submit"
                disabled={isSubmitting}
                isLoading={isSubmitting}
                className="w-full"
              >
                {isSubmitting ? 'Signing in...' : 'Sign In'}
              </Button>
            </form>

            <p className="text-center text-sm text-gray-600 dark:text-gray-400">
              Don't have an account?{' '}
              <a
                href="#"
                className="font-medium text-brand-600 hover:underline dark:text-brand-400"
                onClick={(e) => {
                  e.preventDefault();
                }}
              >
                Sign up
              </a>
            </p>
          </Stack>
        </Card>
      </div>
    </div>
  );
};
