#!/usr/bin/env python3
"""Interactive local SMTP setup. Credentials are never passed in argv or printed."""
import getpass
import json
import os
from pathlib import Path
import re
import smtplib
import ssl
import tempfile


def main():
    root = Path(__file__).resolve().parents[1]
    project = (root / 'backend/TrustFlow.Api/TrustFlow.Api.csproj').read_text(encoding='utf-8-sig')
    identity = re.search(r'<UserSecretsId>([^<]+)</UserSecretsId>', project).group(1)
    directory = Path.home() / '.microsoft/usersecrets' / identity
    secret_file = directory / 'secrets.json'
    print('Configure the LOCAL TrustFlow email sender. Do not paste this password into chat.')
    print('For Gmail, use an App Password for a dedicated sender account, not its normal password.')
    host = input('SMTP host [smtp.gmail.com]: ').strip() or 'smtp.gmail.com'
    port = int(input('SMTP port [587]: ').strip() or '587')
    if port not in (465, 587):
        raise SystemExit('Choose a TLS submission port: 465 or 587.')
    sender = input('Sender email address: ').strip()
    if not re.fullmatch(r'[^\s@]+@[^\s@]+\.[^\s@]+', sender):
        raise SystemExit('Enter a valid sender email address.')
    username = input('SMTP username [same as sender]: ').strip() or sender
    password = getpass.getpass('SMTP App Password (hidden): ')
    if host.lower() == 'smtp.gmail.com':
        password = password.replace(' ', '')
    if not password:
        raise SystemExit('No password supplied; nothing was saved.')
    print('Checking the TLS connection and SMTP authentication. No message will be sent.')
    try:
        if port == 465:
            connection = smtplib.SMTP_SSL(host, port, timeout=15, context=ssl.create_default_context())
        else:
            connection = smtplib.SMTP(host, port, timeout=15)
            connection.ehlo()
            connection.starttls(context=ssl.create_default_context())
            connection.ehlo()
        with connection:
            connection.login(username, password)
    except Exception as error:
        # Provider error text may include sensitive details. Report only the class.
        raise SystemExit(f'Email authentication failed ({type(error).__name__}); nothing was saved.')
    directory.mkdir(parents=True, exist_ok=True)
    current = json.loads(secret_file.read_text(encoding='utf-8-sig')) if secret_file.exists() else {}
    current.update({'Email:Host': host, 'Email:Port': port, 'Email:UseStartTls': True,
                    'Email:FromAddress': sender, 'Email:FromName': 'TrustFlow',
                    'Email:User': username, 'Email:Password': password})
    fd, temporary = tempfile.mkstemp(prefix='.trustflow-email-', dir=directory)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, 'w', encoding='utf-8') as stream:
            json.dump(current, stream, ensure_ascii=False, indent=2)
            stream.write('\n')
        os.replace(temporary, secret_file)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
    print('SMTP authentication succeeded and local settings were saved. Restart the API to use them.')
    print('No email has been sent yet. The next registration or sign-in request will send its code.')


if __name__ == '__main__':
    main()
