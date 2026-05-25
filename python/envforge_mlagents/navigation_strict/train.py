from envforge_mlagents.navigation_strict.patch import apply_navigation_strict_patch


def main() -> None:
    apply_navigation_strict_patch()
    print("EnvForge strict trainer extension: enabled")

    from mlagents.trainers.learn import main as mlagents_main

    mlagents_main()


if __name__ == "__main__":
    main()
