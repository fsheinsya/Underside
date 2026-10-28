pipeline {
    agent any

    options {
        skipDefaultCheckout()
        disableConcurrentBuilds()
    }

    triggers {
        githubPush()
    }

    stages {
        stage('Get source') {
            steps {
                checkout scm
            }
        }

        stage('Test turn events') {
            steps {
                bat '''
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "./ci/Test-TurnEvents.ps1"
exit /b %ERRORLEVEL%
'''
            }
        }
    }
}