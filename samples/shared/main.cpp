#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iostream>

int main(int argc, char** argv) {
    const auto value = std::getenv("DOTENV_SAMPLE");
    const auto untouched = std::getenv("DOTENV_UNTOUCHED");
    const bool loaded = value && std::strcmp(value, "loaded-from-file") == 0;
    const bool requirePreserved = argc > 1 && std::strcmp(argv[1], "--require-preserved") == 0;
    const bool preserved = untouched ? std::strcmp(untouched, "preserved") == 0 : !requirePreserved;
    std::cout << "DotEnv injection: " << (loaded ? "PASS" : "FAIL") << '\n';
    std::cout << "Existing variable: " << (preserved ? "PASS" : "FAIL") << '\n';
    // Contains assertions only, never environment values.
    std::ofstream("dotenv-result.txt") << (loaded && preserved ? "PASS" : "FAIL") << '\n';
    return loaded && preserved ? 0 : 1;
}
