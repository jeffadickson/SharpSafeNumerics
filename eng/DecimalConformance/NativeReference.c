// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

/* Verification-only adapter for unmodified decNumber 3.68 (ICU license).
 * Compile with -DDECNUMDIGITS=2000 for this adapter and decNumber.c alike.
 * One request per process; parse operands at 2000 digits before applying the
 * requested context. Reject parse rounding rather than silently changing input.
 * Output is value<TAB>decNumber-status; these status bits are NOT IEEE bit values.
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <limits.h>
#include "decNumber.h"

/* Strict full-token numeric parsing: atoi would silently accept malformed options. */
static int parse_int(const char *text, int *value) {
    char *end;
    errno = 0;
    long parsed = strtol(text, &end, 10);
    if (errno || end == text || *end || parsed < INT_MIN || parsed > INT_MAX) return 0;
    *value = (int)parsed;
    return 1;
}

int main(int argc, char **argv) {
    if (argc < 9 || argc > 10) return 2;
    decContext context, input;
    decContextDefault(&context, DEC_INIT_BASE);
    context.traps = 0;
    int clamp, rounding;
    if (!parse_int(argv[2], &context.digits) || !parse_int(argv[3], &context.emin) ||
        !parse_int(argv[4], &context.emax) || !parse_int(argv[5], &clamp) ||
        !parse_int(argv[6], &rounding) || clamp < 0 || clamp > 1 ||
        rounding < 0 || rounding >= DEC_ROUND_MAX) return 2;
    context.clamp = (uint8_t)clamp;
    context.round = (enum rounding)rounding;
    if (context.digits < 1 || context.digits > 2000 || context.emin > context.emax ||
        context.clamp > 1 || context.round >= DEC_ROUND_MAX) return 2;
    decContextDefault(&input, DEC_INIT_BASE);
    input.traps = 0; input.digits = 2000;
    decNumber a, b, c, result;
    decNumberFromString(&a, argv[7], &input);
    decNumberFromString(&b, argv[8], &input);
    if (argc > 9) decNumberFromString(&c, argv[9], &input);
    if (input.status != 0) { fprintf(stderr, "operand parse status %u\n", input.status); return 2; }
    const char *op = argv[1];
    if (!strcmp(op, "add")) decNumberAdd(&result, &a, &b, &context);
    else if (!strcmp(op, "subtract")) decNumberSubtract(&result, &a, &b, &context);
    else if (!strcmp(op, "multiply")) decNumberMultiply(&result, &a, &b, &context);
    else if (!strcmp(op, "divide")) decNumberDivide(&result, &a, &b, &context);
    else if (!strcmp(op, "quantize")) decNumberQuantize(&result, &a, &b, &context);
    else if (!strcmp(op, "squareroot")) decNumberSquareRoot(&result, &a, &context);
    else if (!strcmp(op, "exp")) decNumberExp(&result, &a, &context);
    else if (!strcmp(op, "ln")) decNumberLn(&result, &a, &context);
    else if (!strcmp(op, "log10")) decNumberLog10(&result, &a, &context);
    else if (!strcmp(op, "fma") && argc == 10) decNumberFMA(&result, &a, &b, &c, &context);
    else return 2;
    char text[DECNUMDIGITS + 32];
    puts(decNumberToString(&result, text));
    printf("%u\n", context.status);
    return 0;
}
