"use client";

import {
    motion,
    type HTMLMotionProps,
    type Variants,
} from "motion/react";
import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

const fadeUpVariants: Variants = {
    hidden: {
        opacity: 0,
        y: 10,
    },

    visible: {
        opacity: 1,
        y: 0,

        transition: {
            duration: 0.28,
            ease: "easeOut",
        },
    },
};

const staggerContainerVariants: Variants = {
    hidden: {},

    visible: {
        transition: {
            delayChildren: 0.03,
            staggerChildren: 0.05,
        },
    },
};

type AnimatedSectionProps = Omit<
    HTMLMotionProps<"div">,
    "initial" | "animate" | "variants"
> & {
    children: ReactNode;
};

export function AnimatedSection({
    children,
    className,
    ...props
}: AnimatedSectionProps) {
    return (
        <motion.div
            initial="hidden"
            animate="visible"
            variants={fadeUpVariants}
            className={cn(className)}
            {...props}
        >
            {children}
        </motion.div>
    );
}

type StaggerContainerProps = Omit<
    HTMLMotionProps<"div">,
    "initial" | "animate" | "variants"
> & {
    children: ReactNode;
};

export function StaggerContainer({
    children,
    className,
    ...props
}: StaggerContainerProps) {
    return (
        <motion.div
            initial="hidden"
            animate="visible"
            variants={
                staggerContainerVariants
            }
            className={cn(className)}
            {...props}
        >
            {children}
        </motion.div>
    );
}

type StaggerItemProps = Omit<
    HTMLMotionProps<"div">,
    "initial" | "animate" | "variants"
> & {
    children: ReactNode;
};

export function StaggerItem({
    children,
    className,
    ...props
}: StaggerItemProps) {
    return (
        <motion.div
            variants={fadeUpVariants}
            className={cn(className)}
            {...props}
        >
            {children}
        </motion.div>
    );
}