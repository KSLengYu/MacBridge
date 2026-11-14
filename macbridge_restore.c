#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include <setupapi.h>
#include <stdio.h>
#include <stdlib.h>

#include "restore_helper.h"

/** Entry point for dedicated one-click Windows driver restore utility. */
int main(int argc, char **argv) {
    SetConsoleOutputCP(65001);
    setvbuf(stdout, NULL, _IONBF, 0);

    int elev = mb_ensure_admin(argc, argv);
    if (elev > 0) return 0;
    if (elev < 0) return 1;

    int noPause = 0;
    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--no-pause") == 0) noPause = 1;
    }

    printf("============================================================\n");
    printf(" MacBridge-Restore: Restore Apple USB Driver (WinUSB -> usbccgp)\n");
    printf("============================================================\n");

    int rc = mb_restore_apple_driver();

    if (!noPause) {
        printf("\nPress Enter to exit...");
        getchar();
    }
    return rc;
}
